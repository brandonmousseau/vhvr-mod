using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using ValheimVRMod.Scripts.Block;
using ValheimVRMod.Utilities;
using ValheimVRMod.VRCore;
using Valve.VR;

namespace ValheimVRMod.Scripts
{
    class WeaponSecondaryManager : MonoBehaviour
    {
        private ItemDrop.ItemData item;
        private Attack attack;
        private Attack secondaryAttack;
        private Outline outline;
        private bool isRightHand;
        private GameObject parent;

        //secondary attack
        public static Vector3 firstPos;
        private Vector3 lastPos;
        private LineRenderer slashLine;
        private float secondaryAttackTimer;
        private float secondaryAttackTimerFull = -0.5f;
        private bool wasSecondaryAttacked;
        private bool secondaryAttackJustEnded = true;
        private Color slashColor = Color.white;
        private AnimationCurve slashCurve;
        private AnimationCurve circleCurve;
        public static List<Attack.HitPoint> secondaryHitList;
        public static bool wasSecondaryAttack;
        public static Vector3 hitDir;

        private void Awake()
        {
            firstPos = Vector3.zero;
            lastPos = Vector3.zero;

            slashLine = new GameObject().AddComponent<LineRenderer>();
            slashLine.widthMultiplier = 0.02f;
            slashLine.positionCount = 3;
            slashLine.material = new Material(Shader.Find("Custom/AlphaParticle"));
            slashLine.material.color = slashColor;

            slashCurve = new AnimationCurve();
            slashCurve.AddKey(0f, 0.1f);
            slashCurve.AddKey(0.25f, 0.65f);
            slashCurve.AddKey(0.5f, 1f);
            slashCurve.AddKey(0.75f, 0.65f);
            slashCurve.AddKey(1f, 0.1f);

            circleCurve = new AnimationCurve();
            circleCurve.AddKey(0f, 1f);
            circleCurve.AddKey(0.25f, 0.65f);
            circleCurve.AddKey(0.5f, 0.3f);
            circleCurve.AddKey(0.75f, 0.65f);
            circleCurve.AddKey(1f, 1f);

            slashLine.widthCurve = slashCurve;
            slashLine.numCapVertices = 3;
            slashLine.enabled = false;
            slashLine.receiveShadows = false;
            slashLine.shadowCastingMode = ShadowCastingMode.Off;
            slashLine.lightProbeUsage = LightProbeUsage.Off;
            slashLine.reflectionProbeUsage = ReflectionProbeUsage.Off;
            slashLine.loop = false;
        }

        private void OnDestroy()
        {
            Destroy(slashLine);
        }

        private void OnDisable()
        {
            firstPos = Vector3.zero;
            lastPos = Vector3.zero;
            secondaryHitList = new List<Attack.HitPoint>();
            slashLine.enabled = false;
            wasSecondaryAttack = false;
            wasSecondaryAttacked = true;
        }

        public void Initialize(Transform obj, string name, bool rightHand)
        {
            parent = obj.parent.gameObject;
            outline = obj.parent.gameObject.GetComponent<Outline>();
            isRightHand = rightHand;
            if (isRightHand)
            {
                item = Player.m_localPlayer.GetRightItem();
            }
            else
            {
                item = Player.m_localPlayer.GetLeftItem();
            }
            attack = item.m_shared.m_attack.Clone();
            secondaryAttack = item.m_shared.m_secondaryAttack.Clone();
        }
        private void Update()
        {
            if (!outline)
            {
                outline = parent.GetComponent<Outline>();
                return;
            }
            SecondaryAttack();
        }

        private void SecondaryAttack()
        {
            if (attack.m_attackAnimation == "swing_pickaxe")
            {
                secondaryAttack = attack;
            }
            if (secondaryAttack.m_attackAnimation == "")
            {
                return;
            }
            if (secondaryAttackTimer >= secondaryAttackTimerFull)
            {
                secondaryAttackTimer -= Time.deltaTime;
            }

            var mainHand = isRightHand ? VRPlayer.rightHand : VRPlayer.leftHand;
            var offHand = isRightHand ? VRPlayer.leftHand : VRPlayer.rightHand;
            var mainHandSource = isRightHand ? SteamVR_Input_Sources.RightHand : SteamVR_Input_Sources.LeftHand;
            var mainHandTrigger = isRightHand ? SteamVR_Actions.valheim_Use.state : SteamVR_Actions.valheim_UseLeft.state;
            var inCooldown = AttackTargetMeshCooldown.isLastTargetInCooldown();
            var localWeaponForward = WeaponWield.weaponForward * secondaryAttack.m_attackRange / 2;
            var localHandPos = mainHand.transform.position - Player.m_localPlayer.transform.position;
            var posHeight = Player.m_localPlayer.transform.InverseTransformPoint(mainHand.transform.position + localWeaponForward);
            var rangeMultiplier = 1.25f;

            if (WeaponWield.isCurrentlyTwoHanded())
            {
                localHandPos -= WeaponWield.weaponForward * Vector3.Distance(mainHand.transform.position, offHand.transform.position);
            }
            if (!SteamVR_Actions.valheim_Grab.GetState(mainHandSource) || 
                item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon && !WeaponWield.isCurrentlyTwoHanded())
            {
                firstPos = Vector3.zero;
                lastPos = Vector3.zero;
            }
            if (SteamVR_Actions.valheim_Grab.GetState(mainHandSource) && !inCooldown)
            {
                if (firstPos == Vector3.zero && mainHandTrigger)
                {
                    firstPos = localHandPos + localWeaponForward;
                    slashLine.material.color = slashColor;
                    slashLine.widthMultiplier = 0.02f;
                    slashLine.widthCurve = slashCurve;
                    slashLine.loop = false;
                    wasSecondaryAttack = true;
                    hitDir = Vector3.zero;
                }

                if (firstPos != Vector3.zero && !mainHandTrigger)
                {
                    lastPos = localHandPos + localWeaponForward;
                }
            }
            

            if (secondaryAttackTimer <= 0 && firstPos == Vector3.zero)
            {
                var transparency = Color.Lerp(Color.clear, slashColor, Mathf.Max(secondaryAttackTimer + 0.7f, 0) / 0.7f);
                slashLine.widthMultiplier = 0.02f * Mathf.Max(secondaryAttackTimer + 0.5f, 0) / 0.5f;
                slashLine.material.color = transparency;
            }

            if (!(inCooldown || firstPos != Vector3.zero))
            {
                if (secondaryAttackTimer <= secondaryAttackTimerFull)
                {
                    wasSecondaryAttack = false;
                    outline.enabled = false;
                    if (!secondaryAttackJustEnded)
                    {
                        if (isRightHand)
                        {
                            VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 50, 0.1f, SteamVR_Input_Sources.RightHand);
                        }
                        else
                        {
                            VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 50, 0.1f, SteamVR_Input_Sources.LeftHand);
                        }
                        hitDir = Vector3.zero;
                        secondaryAttackJustEnded = true;
                    }
                }
                return;
            }

            List<Vector3> pointList = new List<Vector3>();
            if (secondaryAttack.m_attackAnimation == "atgeir_secondary")
            {
                if (firstPos != Vector3.zero && lastPos == Vector3.zero)
                {
                    var currpos = localHandPos + localWeaponForward;
                    var angle = Vector3.SignedAngle(firstPos, currpos, Player.m_localPlayer.transform.up);
                    var anglestep = angle / 2;
                    var positive = angle >= 0 ? 1 : -1;
                    slashLine.positionCount = ((int)angle * positive) / 4;

                    var modFirstPos = firstPos;
                    modFirstPos.y = 0;
                    var firstDir = modFirstPos.normalized;
                    for (float a = -(anglestep * positive); a <= anglestep * positive; a += 4)
                    {
                        var direction = (Quaternion.AngleAxis((a + (a * 2) + anglestep), Vector3.up) * firstDir).normalized;
                        pointList.Add((Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.up * posHeight.y) + direction * secondaryAttack.m_attackRange);
                    }

                    slashLine.SetPositions(pointList.ToArray());
                    slashLine.enabled = true;
                }
            }
            else
            {
                if (firstPos != Vector3.zero && lastPos == Vector3.zero)
                {
                    pointList.Add(Player.m_localPlayer.transform.position + firstPos);
                    slashLine.positionCount = 3;
                    var currpos = localHandPos + localWeaponForward;
                    var minDist = Mathf.Min(secondaryAttack.m_attackRange * rangeMultiplier, Vector3.Distance(firstPos, currpos) * rangeMultiplier);
                    pointList.Add(Player.m_localPlayer.transform.position + firstPos + ((currpos - firstPos).normalized * minDist / 2));
                    pointList.Add(Player.m_localPlayer.transform.position + firstPos + ((currpos - firstPos).normalized * minDist));
                    slashLine.SetPositions(pointList.ToArray());
                    slashLine.enabled = true;
                }
            }

            if (firstPos != Vector3.zero && lastPos != Vector3.zero && !inCooldown)
            {
                if(Vector3.Distance(firstPos,lastPos) < secondaryAttack.m_attackRange * 0.5f)
                {
                    secondaryAttackTimer = Mathf.Min(GetSecondaryAttackHitTime() / 2, 0.3f);
                    secondaryAttackTimerFull = -GetSecondaryAttackHitTime() + secondaryAttackTimer;
                    firstPos = Vector3.zero;
                    lastPos = Vector3.zero;
                    return;
                }
                int layerMask = secondaryAttack.m_hitTerrain ? Attack.m_attackMaskTerrain : Attack.m_attackMask;
                firstPos = Player.m_localPlayer.transform.position + firstPos;
                lastPos = Player.m_localPlayer.transform.position + lastPos;
                secondaryHitList = new List<Attack.HitPoint>();
                pointList = new List<Vector3>();
                if (secondaryAttack.m_attackAnimation == "atgeir_secondary")
                {
                    slashLine.positionCount = 360 / 4;
                    slashLine.loop = true;
                    slashLine.widthCurve = circleCurve;
                    for (float a = -180; a <= 180; a += 4)
                    {
                        var direction = Player.m_localPlayer.transform.InverseTransformDirection(Quaternion.AngleAxis(a, Vector3.up) * Vector3.forward);
                        RaycastHit[] tempSecondaryHitList = Physics.SphereCastAll(Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.up * posHeight.y, attack.m_attackRayWidth, direction, secondaryAttack.m_attackRange, layerMask, QueryTriggerInteraction.Ignore);
                        Array.Sort<RaycastHit>(tempSecondaryHitList, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
                        RaycastSecondaryAttack(tempSecondaryHitList);
                        pointList.Add((Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.up * posHeight.y) + direction * secondaryAttack.m_attackRange);
                    }
                    slashLine.SetPositions(pointList.ToArray());
                }
                else
                {
                    var minDist = Mathf.Min(secondaryAttack.m_attackRange * rangeMultiplier, Vector3.Distance(firstPos, lastPos) * rangeMultiplier);
                    pointList.Add(firstPos);
                    pointList.Add(firstPos + ((lastPos - firstPos).normalized * minDist / 2));
                    pointList.Add(firstPos + ((lastPos - firstPos).normalized * minDist));
                    slashLine.SetPositions(pointList.ToArray());
                    slashLine.positionCount = 3;
                    hitDir = (lastPos - firstPos).normalized;
                    RaycastHit[] tempSecondaryHitList = Physics.SphereCastAll(firstPos, attack.m_attackRayWidth * 1.5f, (lastPos - firstPos).normalized, minDist, layerMask, QueryTriggerInteraction.Ignore);
                    Array.Sort<RaycastHit>(tempSecondaryHitList, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
                    RaycastSecondaryAttack(tempSecondaryHitList);
                }


                secondaryAttackTimer = Mathf.Min(GetSecondaryAttackHitTime() / 2, 0.3f);
                secondaryAttackTimerFull = -GetSecondaryAttackHitTime() + secondaryAttackTimer;
                if (secondaryHitList.Count >= 1 && Player.m_localPlayer.HaveStamina(getStaminaSecondaryAtttackUsage() + 0.1f))
                {
                    foreach (var hit in secondaryHitList)
                    {
                        var attackTargetMeshCooldown = hit.collider.gameObject.GetComponent<AttackTargetMeshCooldown>();
                        if (attackTargetMeshCooldown == null)
                        {
                            attackTargetMeshCooldown = hit.collider.gameObject.AddComponent<AttackTargetMeshCooldown>();
                        }
                        attackTargetMeshCooldown.tryTrigger(GetSecondaryAttackHitTime());
                    }
                    outline.enabled = true;
                    wasSecondaryAttack = true;
                    wasSecondaryAttacked = false;
                }
                else
                {
                    wasSecondaryAttack = false;
                }
                firstPos = Vector3.zero;
                lastPos = Vector3.zero;
            }


            if (secondaryAttackTimer <= 0 && wasSecondaryAttack && !wasSecondaryAttacked && secondaryHitList != null && secondaryHitList.Count >= 1)
            {
                foreach (var raycastHit in secondaryHitList)
                {
                    StaticObjects.lastHitPoint = raycastHit.firstPoint;
                    StaticObjects.lastHitDir = (lastPos - firstPos).normalized;
                    StaticObjects.lastHitCollider = raycastHit.collider;
                    if (secondaryAttack.Start(Player.m_localPlayer, null, null,
                    Player.m_localPlayer.m_animEvent,
                    null, item, null, 0.0f, 0.0f))
                    {
                        if (isRightHand)
                        {
                            VRPlayer.rightHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.RightHand);
                        }
                        else
                        {
                            VRPlayer.leftHand.hapticAction.Execute(0, 0.2f, 100, 0.5f, SteamVR_Input_Sources.LeftHand);
                        }
                        //bHaptics
                        if (!BhapticsTactsuit.suitDisabled)
                        {
                            BhapticsTactsuit.SwordRecoil(!VHVRConfig.LeftHanded());
                        }
                    }
                }
                wasSecondaryAttacked = true;
                secondaryAttackJustEnded = false;
            }
        }

        private void RaycastSecondaryAttack(RaycastHit[] raycastList)
        {
            if (raycastList.Length <= 0)
            {
                return;
            }

            foreach (var raycastHit in raycastList)
            {
                if (raycastHit.collider.gameObject == Player.m_localPlayer.gameObject)
                {
                    continue;
                }
                GameObject gameObject = Projectile.FindHitObject(raycastHit.collider);
                if (gameObject && !(gameObject == Player.m_localPlayer.gameObject))
                {
                    Vagon component = gameObject.GetComponent<Vagon>();
                    if (!component || !component.IsAttached(Player.m_localPlayer))
                    {
                        Character component2 = gameObject.GetComponent<Character>();
                        if (component2 != null)
                        {
                            bool flag = BaseAI.IsEnemy(Player.m_localPlayer, component2) || (component2.GetBaseAI() && component2.GetBaseAI().IsAggravatable() && Player.m_localPlayer.IsPlayer());
                            if ((!Player.m_localPlayer.IsPlayer() && !flag) || (!item.m_shared.m_tamedOnly && Player.m_localPlayer.IsPlayer() && !Player.m_localPlayer.IsPVPEnabled() && !flag) || (item.m_shared.m_tamedOnly && !component2.IsTamed()))
                            {
                                continue;
                            }
                            if (item.m_shared.m_dodgeable && component2.IsDodgeInvincible())
                            {
                                continue;
                            }
                        }
                        else if (item.m_shared.m_tamedOnly)
                        {
                            continue;
                        }
                        bool multiCollider = secondaryAttack.m_pickaxeSpecial && (gameObject.GetComponent<MineRock5>() || gameObject.GetComponent<MineRock>());
                        secondaryAttack.AddHitPoint(secondaryHitList, gameObject, raycastHit.collider, raycastHit.point, raycastHit.distance, multiCollider);
                        if (!secondaryAttack.m_hitThroughWalls && Vector3.Distance(raycastList[0].point, raycastHit.point) >= 0.3f)
                        {
                            break;
                        }
                    }
                }
            }
        }

        private float GetSecondaryAttackHitTime()
        {
            switch (secondaryAttack.m_attackAnimation)
            {
                case "axe_secondary":
                    return 1.8f;
                case "atgeir_secondary":
                case "mace_secondary":
                case "sword_secondary":
                case "greatsword_secondary":
                    return 2f;
                case "knife_secondary":
                case "dual_knives_secondary":
                    return 1.5f;
                case "battleaxe_secondary":
                    return 0.86f;
                case "swing_pickaxe":
                    return 1.3f;
                default:
                    return 2f;
            }
        }
        private float getStaminaSecondaryAtttackUsage()
        {

            if (secondaryAttack.m_attackStamina <= 0.0)
            {
                return 0.0f;
            }
            double attackStamina = secondaryAttack.m_attackStamina;
            return (float)(attackStamina - attackStamina * 0.330000013113022 * Player.m_localPlayer.GetSkillFactor(item.m_shared.m_skillType));
        }
    }
}
