using ValheimVRMod.VRCore.Backends;
using System.Collections;
using UnityEngine.Networking;
using UnityEngine;
using ValheimVRMod.Utilities;
using System;

namespace ValheimVRMod.VRCore.UI
{
    public class VoiceChat : MonoBehaviour
    {
        private enum TalkGesture
        {
            IDLE,
            START,
            RELEASE
        }
        private const string GROQ_URL = "https://api.groq.com/openai/v1/audio/transcriptions";
        private const string RECORDING_MESSAGE = "Recording voice chat...";
        private const float FAILURE_MESSAGE_DURATION = 1f;
        // How loud (RMS, of the full scale of 1) the loudest moment of a recording has to be to count as speech.
        // A recording without speech is not sent, since it gets transcribed to made up words like "Thank you.".
        private const float MIN_SPEECH_LOUDNESS = 0.01f;
        private const float SPEECH_LOUDNESS_WINDOW = 0.1f;
        private static readonly Color SHOUT_INDICATOR_COLOR = new Color(1f, 0.5f, 0f);
        private static readonly Vector3 SHOUT_INDICATOR_POSITION = new Vector3(0, 0, 0.75f);
        private const float SHOUT_INDICATOR_DIAMETER = 0.06f;

        private AudioClip recording;
        private bool isRecording = false;
        private bool hasPending = false;
        private bool isLastRecordingShout = false;
        // How many sample frames of the recording clip have actually been recorded.
        private int recordedSamples = 0;
        private string pendingText = null;
        // Why the last recording did not make it to the chat, until Update() has picked it up.
        private string pendingFailure = null;
        private string failureMessage = null;
        private float failureMessageTimer = 0;
        // The text that this has put on the message hud, if it is showing any.
        private string shownStatus = null;
        // A disk in front of the eyes while a recording would be shouted, since the message hud is out of sight when
        // looking up.
        private MeshRenderer shoutIndicator;
        private readonly object lockObj = new object();

        void Update()
        {
            if (VHVRConfig.GroqApiKey() == "")
            {
                return;
            }

            var talkGesture = GetTalkGesture();

            bool bothGrab = VRInputActions.valheim_Grab.GetState(VRInputSource.LeftHand) &&
                            VRInputActions.valheim_Grab.GetState(VRInputSource.RightHand);

            if (isRecording)
            {
                if (!Microphone.IsRecording(null))
                {
                    LogUtils.LogDebug("Chat recording timeout");
                    isLastRecordingShout = false;
                    isRecording = false;
                    recordedSamples = recording.samples;
                    StartCoroutine(SendToGroq());
                }
                else if (talkGesture == TalkGesture.RELEASE)
                {
                    LogUtils.LogDebug("Stop recording chat");
                    isLastRecordingShout = IsInShoutPose();
                    int lastSamplePos = Microphone.GetPosition(null);
                    Microphone.End(null);
                    recordedSamples = lastSamplePos;
                    float duration = (float)lastSamplePos / recording.frequency;
                    isRecording = false;
                    if (duration > 0.5f)
                    {
                        LogUtils.LogDebug("Voice recording length: " + recording.length);
                        StartCoroutine(SendToGroq());
                    }
                    else
                    {
                        LogUtils.LogDebug("Voice recording too short to transcribe");
                        hasPending = false;
                        ShowFailure("Voice chat: too short");
                    }
                }
            }
            else if (talkGesture == TalkGesture.START && !hasPending)
            {
                LogUtils.LogDebug("Start recording chat");
                recording = Microphone.Start(null, false, 30, 16000);
                isRecording = true;
                hasPending = true;
            }

            string failure = ConsumePendingFailure();
            if (failure != null)
            {
                // Otherwise the next recording could never start.
                hasPending = false;
                ShowFailure(failure);
            }

            UpdateStatus();
            UpdateShoutIndicator();

            if (hasPending && !isRecording && Chat.instance != null)
            {
                // Shows the chat window, without its text input, while waiting for the recording to be transcribed.
                Chat.instance.m_hideTimer = 0f;
            }

            string text = ConsumePending();
            if (text == null || Chat.instance == null)
            {
                return;
            }

            Chat.instance.SendText(isLastRecordingShout ? Talker.Type.Shout : Talker.Type.Normal, text);
        }

        private IEnumerator SendToGroq()
        {
            // Only send what was actually recorded, not the unused rest of the clip.
            int samples = Mathf.Clamp(recordedSamples, 1, recording.samples) * recording.channels;
            float[] audioData = new float[samples];
            recording.GetData(audioData, 0);
            if (!ContainsSpeech(audioData, recording.frequency * recording.channels))
            {
                LogUtils.LogDebug("Voice recording too quiet to transcribe");
                lock (lockObj) { pendingFailure = "Voice chat: nothing recognized"; }
                yield break;
            }
            byte[] wavBytes = ToWav(audioData, recording.frequency, recording.channels);

            WWWForm form = new WWWForm();
            form.AddBinaryData("file", wavBytes, "audio.wav", "audio/wav");
            form.AddField("model", "whisper-large-v3-turbo");

            using (UnityWebRequest req = UnityWebRequest.Post(GROQ_URL, form))
            {
                req.SetRequestHeader("Authorization", "Bearer " + VHVRConfig.GroqApiKey());
                yield return req.SendWebRequest();

                if (req.result == UnityWebRequest.Result.Success)
                {
                    string json = req.downloadHandler.text;
                    LogUtils.LogDebug("Voice chat response: " + json);
                    // parse {"text":"..."}
                    string text = json.Split('"')[3];
                    if (!string.IsNullOrEmpty(text))
                    {
                        LogUtils.LogDebug("Voice chat recognized: " + text);
                        lock (lockObj) { pendingText = text; }
                    }
                    else
                    {
                        lock (lockObj) { pendingFailure = "Voice chat: nothing recognized"; }
                    }
                }
                else
                {
                    LogUtils.LogWarning("Voice chat request failed: " + req.error + " / " + req.downloadHandler.text);
                    lock (lockObj) { pendingFailure = "Voice chat failed"; }
                }
            }
        }

        private string ConsumePending()
        {
            lock (lockObj)
            {
                string text = null;
                if (hasPending && pendingText != null)
                {
                    text = pendingText;
                    hasPending = false;
                }
                pendingText = null;
                return text;
            }
        }

        // Require looking upward to use shout
        private static bool IsInShoutPose()
        {
            return Vector3.Dot(VRPlayer.vrCam.transform.forward, VRPlayer.vrCam.transform.parent.up) > 0.75f;
        }

        private void UpdateShoutIndicator()
        {
            bool show = isRecording && IsInShoutPose();
            if (shoutIndicator == null)
            {
                if (!show)
                {
                    return;
                }
                shoutIndicator = GameObject.CreatePrimitive(PrimitiveType.Cylinder).GetComponent<MeshRenderer>();
                shoutIndicator.name = "ShoutIndicator";
                Destroy(shoutIndicator.GetComponent<Collider>());
                shoutIndicator.material = Instantiate(VRAssetManager.GetAsset<Material>("Unlit"));
                shoutIndicator.material.color = SHOUT_INDICATOR_COLOR;
                shoutIndicator.gameObject.layer = LayerUtils.getWorldspaceUiLayer();
                shoutIndicator.receiveShadows = false;
                shoutIndicator.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Transform head = VRPlayer.vrCam.transform;
            if (shoutIndicator.transform.parent != head)
            {
                // The flat sides of the cylinder face the eyes.
                shoutIndicator.transform.SetParent(head, false);
                shoutIndicator.transform.localPosition = SHOUT_INDICATOR_POSITION;
                shoutIndicator.transform.localRotation = Quaternion.Euler(90, 0, 0);
                shoutIndicator.transform.localScale =
                    new Vector3(SHOUT_INDICATOR_DIAMETER, 0.001f, SHOUT_INDICATOR_DIAMETER);
            }
            shoutIndicator.enabled = show;
        }

        void OnDestroy()
        {
            if (shoutIndicator != null)
            {
                Destroy(shoutIndicator.gameObject);
            }
        }

        private string ConsumePendingFailure()
        {
            lock (lockObj)
            {
                string failure = pendingFailure;
                pendingFailure = null;
                return failure;
            }
        }

        private void ShowFailure(string message)
        {
            failureMessage = message;
            failureMessageTimer = FAILURE_MESSAGE_DURATION;
        }

        // Shows on the message hud that a recording is in progress or, for a moment, that it failed.
        private void UpdateStatus()
        {
            string status = null;
            if (isRecording)
            {
                status = RECORDING_MESSAGE;
            }
            else if (failureMessageTimer > 0)
            {
                failureMessageTimer -= Time.unscaledDeltaTime;
                status = failureMessage;
            }

            var centerText = MessageHud.instance == null ? null : MessageHud.instance.m_messageCenterText;
            if (centerText == null)
            {
                shownStatus = null;
                return;
            }

            if (status != null && !Hud.IsUserHidden())
            {
                // Not MessageHud.ShowMessage(), which fades the message out by itself and logs it.
                centerText.text = status;
                centerText.CrossFadeAlpha(1f, 0f, true);
                shownStatus = status;
            }
            else if (shownStatus != null)
            {
                // Any other message that has been shown in the meantime stays.
                if (centerText.text == shownStatus)
                {
                    centerText.CrossFadeAlpha(0f, 0.25f, true);
                }
                shownStatus = null;
            }
        }

        private TalkGesture GetTalkGesture()
        {
            Transform head = VRPlayer.vrCam.transform;
            Transform leftHand = VRPlayer.leftHand.transform;
            Transform rightHand = VRPlayer.rightHand.transform;
            bool isLeftHandInMouthRange = IsHandInMouthRange(leftHand, head);
            bool isRightHandInMouthRange = IsHandInMouthRange(rightHand, head);

            if (!isLeftHandInMouthRange && !isRightHandInMouthRange)
            {
                return TalkGesture.RELEASE;
            }

            if (!isLeftHandInMouthRange || !isRightHandInMouthRange)
            {
                return TalkGesture.IDLE;
            }

            if (Vector3.Dot(leftHand.right, head.right) < 0.875f || // left palm not facing right
                Vector3.Dot(rightHand.right, head.right) < 0.875f || // right palm not facing left
                Vector3.Dot(leftHand.up, head.up) > -0.75 || // left digital not pointing up
                Vector3.Dot(rightHand.up, head.up) > -0.75) // right digital not pointing up
            {
                return TalkGesture.IDLE;
            }

            return TalkGesture.START;
        }

        private bool IsHandInMouthRange(Transform hand, Transform head)
        {
            Vector3 offset = hand.position - head.position;

            float verticalOffset = Vector3.Dot(offset, head.up);
            if (verticalOffset > 0 || verticalOffset < -0.125)
            {
                return false;
            }
            float anteriorOffset = Vector3.Dot(offset, head.forward);
            if (anteriorOffset < 0 || anteriorOffset > 0.125f)
            {
                return false;
            }
            float lateralOffset = Vector3.Dot(offset, head.right);
            return Mathf.Abs(lateralOffset) < 0.125f;
        }

        // Whether any moment of the recording is loud enough to be someone speaking.
        private static bool ContainsSpeech(float[] samples, int samplesPerSecond)
        {
            int window = Mathf.Max(1, (int)(samplesPerSecond * SPEECH_LOUDNESS_WINDOW));
            float minSquareSum = MIN_SPEECH_LOUDNESS * MIN_SPEECH_LOUDNESS * window;
            for (int start = 0; start + window <= samples.Length; start += window)
            {
                float squareSum = 0;
                for (int i = start; i < start + window; i++)
                {
                    squareSum += samples[i] * samples[i];
                }
                if (squareSum >= minSquareSum)
                {
                    return true;
                }
            }
            return false;
        }

        // Minimal WAV encoder
        private static byte[] ToWav(float[] samples, int frequency, int channels)
        {
            using (var ms = new System.IO.MemoryStream())
            using (var writer = new System.IO.BinaryWriter(ms))
            {
                int sampleCount = samples.Length;
                int byteCount = sampleCount * 2; // 16-bit

                // WAV header
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + byteCount);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                writer.Write(16);           // chunk size
                writer.Write((short)1);     // PCM
                writer.Write((short)channels);
                writer.Write(frequency);
                writer.Write(frequency * channels * 2); // byte rate
                writer.Write((short)(channels * 2));    // block align
                writer.Write((short)16);    // bits per sample
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                writer.Write(byteCount);

                // Samples
                foreach (float s in samples)
                {
                    writer.Write((short)(Mathf.Clamp(s, -1f, 1f) * 32767));
                }

                return ms.ToArray();
            }
        }
    }
}
