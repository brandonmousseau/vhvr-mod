using System.Collections;
using UnityEngine.Networking;
using UnityEngine;
using ValheimVRMod.Utilities;
using Valve.VR;
using System;

namespace ValheimVRMod.VRCore.UI
{
    public class VoiceChat : MonoBehaviour
    {
        private enum TalkGesture
        {
            IDLE,
            NORMAL,
            SHOUT,
            RELEASE
        }
        private const string GROQ_URL = "https://api.groq.com/openai/v1/audio/transcriptions";

        private AudioClip recording;
        private bool isRecording = false;
        private bool isLastRecordingShout = false;
        private string pendingText = null;
        private readonly object lockObj = new object();

        void Update()
        {
            if (VHVRConfig.GroqApiKey() == "")
            {
                return;
            }

            var talkGesture = GetTalkGesture();

            bool bothGrab = SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.LeftHand) &&
                            SteamVR_Actions.valheim_Grab.GetState(SteamVR_Input_Sources.RightHand);

            if (isRecording)
            {
                if (talkGesture == TalkGesture.RELEASE)
                {
                    StopRecording();
                }
                else if (talkGesture == TalkGesture.SHOUT)
                {
                    isLastRecordingShout = true;
                }
            }
            else if (talkGesture == TalkGesture.NORMAL)
            {
                isLastRecordingShout = false;
                StartRecording();
            }
            else if (talkGesture == TalkGesture.SHOUT) {
                isLastRecordingShout = true;
                StartRecording();

            }

            string text = ConsumePending();
            if (text == null || Chat.instance == null)
            {
                return;
            }

            Chat.instance.SendText(isLastRecordingShout ? Talker.Type.Shout : Talker.Type.Normal, text);
        }

        private void StartRecording()
        {
            LogUtils.LogDebug("Start recording chat");
            recording = Microphone.Start(null, false, 30, 16000);
            isRecording = true;
        }

        private void StopRecording()
        {
            LogUtils.LogDebug("Stop recording chat");
            Microphone.End(null);
            isRecording = false;
            StartCoroutine(SendToGroq());
        }

        private IEnumerator SendToGroq()
        {
            // Trim silence - only send what was actually recorded
            int samples = recording.samples * recording.channels;
            float[] audioData = new float[samples];
            recording.GetData(audioData, 0);
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
                }
                else
                {
                    LogUtils.LogWarning("Voice chat request failed: " + req.error + " / " + req.downloadHandler.text);
                }
            }
        }

        private string ConsumePending()
        {
            lock (lockObj)
            {
                string text = pendingText;
                pendingText = null;
                return text;
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

            bool palmsFacingEachOther =
                Vector3.Dot(leftHand.right, head.right) > 0.75f &&
                Vector3.Dot(rightHand.right, head.right) > 0.75f;
            if (!palmsFacingEachOther)
            {
                return TalkGesture.IDLE;
            }

            Vector3 leftHandDistal = leftHand.forward - leftHand.up;
            Vector3 rightHandDistal = rightHand.forward - rightHand.up;
            return leftHandDistal.y > 1.25f && rightHandDistal.y > 1.25f ? TalkGesture.SHOUT : TalkGesture.NORMAL;
        }

        private bool IsHandInMouthRange(Transform hand, Transform head)
        {
            Vector3 offset = hand.position - head.position;
            if (offset.y > 0 || offset.y < -0.25)
            {
                return false;
            }
            float anteriorOffset = Vector3.Dot(head.forward, offset);
            if (anteriorOffset < 0 || anteriorOffset > 0.25f)
            {
                return false;
            }
            return Mathf.Abs(Vector3.Dot(head.right, offset)) < 0.125f;
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
