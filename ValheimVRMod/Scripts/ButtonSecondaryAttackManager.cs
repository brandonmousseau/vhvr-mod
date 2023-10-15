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
    class ButtonSecondaryAttackManager : MonoBehaviour
    {
        private ItemDrop.ItemData item;
        private Attack attack;
        private Attack secondaryAttack;
        private Outline outline;
        private bool isRightHand;
        private GameObject parent;
        private bool isSecondaryAvailable;

        //secondary attack
        public static Vector3 firstPos;
        private Vector3 lastPos;
        private LineRenderer slashLine;
        private TrailRenderer slashTrail;
        private float secondaryAttackTimer;
        private float secondaryAttackTimerFull = -0.5f;
        public static bool isSecondaryAttackStarted;
        private bool isSecondaryAttackTriggered;
        private bool isSecondaryAttackEnded = true;
        private Color slashColor = Color.white;
        private AnimationCurve slashCurve;
        private AnimationCurve circleCurve;
        public static List<Attack.HitPoint> secondaryHitList;
        public static int terrainHitCount;
        public static Vector3 hitDir;
        private float rangeMultiplier;
        private List<Vector3> lastpointList;
        private Vector3 pointVel1;
        private Vector3 pointVel2;
        private Vector3 pointVel3;
        private Vector3 pointVel4;
        private Vector3 pointVel5;

        private void Awake()
        {
            firstPos = Vector3.zero;
            lastPos = Vector3.zero;

            slashLine = new GameObject("VHVRSecondaryAttackLine").AddComponent<LineRenderer>();
            slashLine.widthMultiplier = 0.02f;
            slashLine.positionCount = 5;
            slashLine.material = new Material(Shader.Find("Custom/AlphaParticle"));
            slashLine.material.color = slashColor;

            circleCurve = new AnimationCurve();
            circleCurve.AddKey(0f, 1f);
            circleCurve.AddKey(0.25f, 0.65f);
            circleCurve.AddKey(0.5f, 0.3f);
            circleCurve.AddKey(0.75f, 0.65f);
            circleCurve.AddKey(1f, 1f);

            
            slashLine.numCapVertices = 1;
            slashLine.enabled = false;
            slashLine.receiveShadows = false;
            slashLine.shadowCastingMode = ShadowCastingMode.Off;
            slashLine.lightProbeUsage = LightProbeUsage.Off;
            slashLine.reflectionProbeUsage = ReflectionProbeUsage.Off;
            slashLine.loop = false;


            slashTrail = new GameObject("VHVRSecondaryAttackTrail").AddComponent<TrailRenderer>();
            slashTrail.widthMultiplier = 0f;
            slashTrail.material = new Material(Shader.Find("Custom/AlphaParticle"));
            slashTrail.material.color = Color.clear;
            slashTrail.numCapVertices = 3;
            slashTrail.receiveShadows = false;
            slashTrail.shadowCastingMode = ShadowCastingMode.Off;
            slashTrail.lightProbeUsage = LightProbeUsage.Off;
            slashTrail.reflectionProbeUsage = ReflectionProbeUsage.Off;
            slashTrail.time = 0.25f;
            slashTrail.numCornerVertices = 3;
            slashTrail.minVertexDistance = 0.05f;
            slashTrail.emitting = false;
        }

        private void OnDestroy()
        {
            Destroy(slashLine);
            Destroy(slashTrail);
        }

        private void OnDisable()
        {
            firstPos = Vector3.zero;
            lastPos = Vector3.zero;
            secondaryHitList = new List<Attack.HitPoint>();
            slashLine.enabled = false;
            isSecondaryAttackStarted = false;
            isSecondaryAttackTriggered = true;
        }

        public void Initialize(Transform obj, string name, bool isRightHand)
        {
            parent = obj.parent.gameObject;
            outline = obj.parent.gameObject.GetComponent<Outline>();
            this.isRightHand = isRightHand;
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

            
            float damage = 0;
            if (item.m_shared.m_damages.m_slash > damage)
            {
                slashCurve = new AnimationCurve();
                slashCurve.AddKey(0f, 0.1f);
                slashCurve.AddKey(0.25f, 0.65f);
                slashCurve.AddKey(0.5f, 1f);
                slashCurve.AddKey(0.75f, 0.65f);
                slashCurve.AddKey(1f, 0.1f);
                damage = item.m_shared.m_damages.m_slash;
            }
            if(item.m_shared.m_damages.m_pierce > damage)
            {
                slashCurve = new AnimationCurve();
                slashCurve.AddKey(0f, 0.3f);
                slashCurve.AddKey(0.25f, 1f);
                slashCurve.AddKey(1f, 0.1f);
                damage = item.m_shared.m_damages.m_pierce;
            }
            if (item.m_shared.m_damages.m_blunt > damage)
            {
                slashCurve = new AnimationCurve();
                slashCurve.AddKey(0f, 0.1f);
                slashCurve.AddKey(0.5f, 0.5f);
                slashCurve.AddKey(1f, 1f);
                damage = item.m_shared.m_damages.m_blunt;
            }

            if (damage == 0)
            {
                slashCurve = new AnimationCurve();
                slashCurve.AddKey(0f, 0.1f);
                slashCurve.AddKey(0.25f, 0.65f);
                slashCurve.AddKey(0.5f, 1f);
                slashCurve.AddKey(0.75f, 0.65f);
                slashCurve.AddKey(1f, 0.1f);
            }

            slashLine.widthCurve = slashCurve;
            isSecondaryAvailable = true;
            rangeMultiplier = 1f;
            slashTrail.Clear();

            if (attack.m_attackAnimation == "swing_pickaxe")
            {
                secondaryAttack = attack;
                rangeMultiplier = 2f;
                slashTrail.time = 0.4f;
            }
            if(secondaryAttack.m_attackAnimation == "knife_secondary")
            {
                rangeMultiplier = 1.5f;
            }
            if (secondaryAttack.m_attackAnimation == "" || obj.gameObject.GetComponent<ThrowableManager>() != null)
            {
                isSecondaryAvailable = false;
            }
            
        }
        private void Update()
        {
            if (!outline)
            {
                outline = parent.GetComponent<Outline>();
                return;
            }
            UpdateSecondaryAttack();
        }

        private void UpdateSecondaryAttack()
        {

            if (!isSecondaryAvailable)
            {
                return;
            }

            if (secondaryAttackTimer >= secondaryAttackTimerFull)
            {
                secondaryAttackTimer -= Time.deltaTime;
            }
            
            var mainHandTrigger = isRightHand ? SteamVR_Actions.valheim_Use.state : SteamVR_Actions.valheim_UseLeft.state;
            var inCooldown = AttackTargetMeshCooldown.isPrimaryTargetInCooldown();
            var localWeaponForward = LocalWeaponWield.weaponForward * secondaryAttack.m_attackRange / 2;
            var localHandPos = VRPlayer.dominantHand.transform.position - Player.m_localPlayer.transform.position;
            var posHeight = Player.m_localPlayer.transform.InverseTransformPoint(VRPlayer.dominantHand.transform.position + localWeaponForward);

            if (LocalWeaponWield.isCurrentlyTwoHanded())
            {
                localHandPos -= LocalWeaponWield.weaponForward * Vector3.Distance(VRPlayer.dominantHand.transform.position, VRPlayer.dominantHand.otherHand.transform.position);
            }
            if (!SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource) || 
                item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon && !LocalWeaponWield.isCurrentlyTwoHanded())
            {
                firstPos = Vector3.zero;
                lastPos = Vector3.zero;
            }
            
            //Input Check
            if (SteamVR_Actions.valheim_Grab.GetState(VRPlayer.dominantHandInputSource) && 
                !inCooldown && 
                !VRPlayer.IsClickableGuiOpen && 
                !(item.m_shared.m_itemType == ItemDrop.ItemData.ItemType.TwoHandedWeapon && !LocalWeaponWield.isCurrentlyTwoHanded()))
            {
                if (firstPos == Vector3.zero && mainHandTrigger)
                {
                    firstPos = localHandPos + localWeaponForward;
                    slashLine.material.color = slashColor;
                    slashLine.widthMultiplier = 0.02f;
                    slashLine.widthCurve = slashCurve;
                    slashLine.loop = false;

                    slashTrail.transform.SetParent(VRPlayer.dominantHand.transform);
                    slashTrail.transform.position = Player.m_localPlayer.transform.position + firstPos;
                    slashTrail.material.color = Color.clear;
                    slashTrail.emitting = true;
                    slashTrail.Clear();

                    isSecondaryAttackStarted = true;
                    hitDir = Vector3.zero;
                }

                if (firstPos != Vector3.zero && !mainHandTrigger)
                {
                    lastPos = localHandPos + localWeaponForward;
                }
            }
            
            //Timer Check for slash line fadeout
            if (secondaryAttackTimer <= 0 && firstPos == Vector3.zero)
            {
                var transparency = Color.Lerp(Color.clear, slashColor, Mathf.Max(secondaryAttackTimer + 0.7f, 0) / 0.7f);
                slashLine.widthMultiplier = 0.02f * Mathf.Max(secondaryAttackTimer + 0.5f, 0) / 0.5f;
                slashLine.material.color = transparency;
            }

            //reset variables and trigger vibration when cooldown ended
            if (!(inCooldown || firstPos != Vector3.zero))
            {
                if (secondaryAttackTimer <= secondaryAttackTimerFull)
                {
                    isSecondaryAttackStarted = false;
                    slashTrail.emitting = false;
                    outline.enabled = false;
                    if (!isSecondaryAttackEnded)
                    {
                        VRPlayer.dominantHand.hapticAction.Execute(0, 0.2f, 50, 0.1f, VRPlayer.dominantHandInputSource);
                        hitDir = Vector3.zero;
                        isSecondaryAttackEnded = true;
                    }
                }
                return;
            }

            List<Vector3> pointList = new List<Vector3>();
            if(lastpointList == null) 
                lastpointList = new List<Vector3>();

            //Rendering line when input is held
            if (firstPos != Vector3.zero && lastPos == Vector3.zero)
            {
                if (secondaryAttack.m_attackAnimation == "atgeir_secondary")
                {
                    var currpos = localHandPos + localWeaponForward;
                    var angle = Vector3.SignedAngle(firstPos, currpos, Player.m_localPlayer.transform.up);
                    var anglestep = angle / 2;
                    slashLine.positionCount = ((int)Mathf.Abs(angle)) / 4;

                    var modFirstPos = firstPos;
                    modFirstPos.y = 0;
                    var firstDir = modFirstPos.normalized;
                    for (float a = -(Mathf.Abs(anglestep)); a <= Mathf.Abs(anglestep); a += 4)
                    {
                        var direction = (Quaternion.AngleAxis((a + (a * 2) + anglestep), Vector3.up) * firstDir).normalized;
                        pointList.Add((Player.m_localPlayer.transform.position + Player.m_localPlayer.transform.up * posHeight.y) + direction * secondaryAttack.m_attackRange);
                    }

                    slashLine.SetPositions(pointList.ToArray());
                    slashLine.enabled = true;
                }
                else
                {
                    var currpos = localHandPos + localWeaponForward;
                    slashTrail.transform.position = Player.m_localPlayer.transform.position + currpos;
                    slashTrail.enabled = true;
                    slashLine.positionCount = 5;
                    if (slashTrail.positionCount > 5)
                    {
                        var firstTrail = slashTrail.GetPosition(0);
                        var halfTrail = slashTrail.GetPosition((int)((slashTrail.positionCount - 1) * 0.5f));
                        var endTrail = slashTrail.GetPosition(slashTrail.positionCount - 1);

                        if (lastpointList.Count != 0)
                        {
                            pointList.Add(Vector3.SmoothDamp(lastpointList[0], firstTrail, ref pointVel1, 0.06f));
                            pointList.Add(Vector3.SmoothDamp(lastpointList[1], (firstTrail + halfTrail) / 2, ref pointVel2, 0.06f));
                            pointList.Add(Vector3.SmoothDamp(lastpointList[2], halfTrail, ref pointVel3, 0.06f));
                            pointList.Add(Vector3.SmoothDamp(lastpointList[3], (halfTrail + endTrail) / 2, ref pointVel4, 0.06f));
                            if (rangeMultiplier == 1)
                            {
                                pointList.Add(Vector3.SmoothDamp(lastpointList[4], endTrail, ref pointVel5, 0.06f));
                            }
                            else
                            {
                                pointList.Add(Vector3.SmoothDamp(lastpointList[4], halfTrail + ((endTrail - halfTrail) * rangeMultiplier), ref pointVel5, 0.06f));
                            }
                            
                        }
                        else
                        {
                            pointList.Add(firstTrail);
                            pointList.Add((firstTrail + halfTrail) / 2);
                            pointList.Add(halfTrail);
                            pointList.Add((halfTrail + endTrail) / 2);
                            pointList.Add(endTrail);
                        }

                        slashLine.SetPositions(pointList.ToArray());
                        slashLine.enabled = true;
                        lastpointList = pointList;
                    }
                    else
                    {
                        slashLine.enabled = false;
                    }
                }
            }

            //Secondary attack check after input is hold and then released
            if (firstPos != Vector3.zero && lastPos != Vector3.zero && !inCooldown)
            {
                int layerMask = secondaryAttack.m_hitTerrain ? Attack.m_attackMaskTerrain : Attack.m_attackMask;
                firstPos = Player.m_localPlayer.transform.position + firstPos;
                lastPos = Player.m_localPlayer.transform.position + lastPos;
                secondaryHitList = new List<Attack.HitPoint>();
                terrainHitCount = 0;
                pointList = new List<Vector3>();

                //Secondary attack raycast check
                if (secondaryAttack.m_attackAnimation == "atgeir_secondary")
                {
                    if (Vector3.Distance(firstPos, lastPos) < secondaryAttack.m_attackRange * 0.5f)
                    {
                        ResetSecondaryAttack();
                        return;
                    }
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
                    var firstTrail = slashTrail.GetPosition(0);
                    var halfTrail = slashTrail.GetPosition((int)((slashTrail.positionCount - 1) * 0.5f));
                    var endTrail = slashTrail.GetPosition(slashTrail.positionCount - 1);
                    pointList.Add(firstTrail);
                    pointList.Add((firstTrail + halfTrail) / 2);
                    pointList.Add(halfTrail);
                    pointList.Add((halfTrail + endTrail) / 2);
                    if (rangeMultiplier == 1)
                    {
                        pointList.Add(endTrail);
                    }
                    else
                    {
                        pointList.Add(halfTrail + ((endTrail - halfTrail) * rangeMultiplier));
                    }
                    slashLine.SetPositions(pointList.ToArray());
                    slashLine.positionCount = 5;

                    if ((Vector3.Distance(firstTrail, halfTrail) + Vector3.Distance(halfTrail, endTrail)) < secondaryAttack.m_attackRange * 0.5f)
                    {
                        ResetSecondaryAttack();
                        return;
                    }
                    
                    hitDir = (endTrail - halfTrail).normalized;
                    var rayWidth = secondaryAttack.m_attackRayWidth == 0 ? attack.m_attackRayWidth : secondaryAttack.m_attackRayWidth;

                    RaycastHit[] tempSecondaryHitList = Physics.SphereCastAll(firstTrail, rayWidth * 1.25f, (halfTrail - firstTrail).normalized, Vector3.Distance(firstTrail,halfTrail), layerMask, QueryTriggerInteraction.Ignore);
                    Array.Sort<RaycastHit>(tempSecondaryHitList, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
                    RaycastSecondaryAttack(tempSecondaryHitList);

                    
                    tempSecondaryHitList = Physics.SphereCastAll(halfTrail, rayWidth * 1.25f, (endTrail - halfTrail).normalized, Vector3.Distance(halfTrail, endTrail) * rangeMultiplier, layerMask, QueryTriggerInteraction.Ignore);
                    Array.Sort<RaycastHit>(tempSecondaryHitList, (RaycastHit x, RaycastHit y) => x.distance.CompareTo(y.distance));
                    RaycastSecondaryAttack(tempSecondaryHitList);
                }

                var hitTime = WeaponUtils.GetAttackDuration(secondaryAttack);
                secondaryAttackTimer = Mathf.Min(hitTime / 2, 0.3f);
                secondaryAttackTimerFull = -hitTime + secondaryAttackTimer;

                //Secondary attack check target outlines and terrain hit
                if (secondaryHitList.Count >= 1 && Player.m_localPlayer.HaveStamina(getStaminaSecondaryAtttackUsage() + 0.1f))
                {
                    var isTerrain = item.m_shared.m_spawnOnHitTerrain ? true : false;
                    foreach (var hit in secondaryHitList)
                    {
                        var target = hit.collider.gameObject;
                        if (target.GetComponent<Heightmap>() == null)
                        {
                            isTerrain = false;
                        }
                        else
                        {
                            terrainHitCount += 1;
                        }

                        var character = hit.collider.gameObject.GetComponentInParent<Character>();
                        if (character != null)
                        {
                            target = character.gameObject;
                        }

                        var attackTargetMeshCooldown = target.GetComponent<AttackTargetMeshCooldown>();
                        if (attackTargetMeshCooldown == null)
                        {
                            attackTargetMeshCooldown = target.AddComponent<AttackTargetMeshCooldown>();
                        }
                        attackTargetMeshCooldown.tryTriggerSecondaryAttack(hitTime,false);
                    }
                    if (isTerrain)
                    {
                        MainWeaponCollision weaponCollision = Player.m_localPlayer.gameObject.GetComponentInChildren<MainWeaponCollision>();
                        weaponCollision.isLastHitOnTerrain = true;
                    }
                    outline.enabled = true;
                    outline.OutlineColor = new Color(1, 1, 0, 0.5f);
                    outline.OutlineWidth = 10;
                    isSecondaryAttackStarted = true;
                    isSecondaryAttackTriggered = false;
                }
                else
                {
                    ResetSecondaryAttack();
                }
                slashTrail.emitting = false;
                firstPos = Vector3.zero;
                lastPos = Vector3.zero;
                lastpointList = new List<Vector3>();
            }

            //actual damage to enemy part
            if (secondaryAttackTimer <= 0 && isSecondaryAttackStarted && !isSecondaryAttackTriggered && secondaryHitList != null && secondaryHitList.Count >= 1)
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
                    }
                }
                isSecondaryAttackTriggered = true;
                isSecondaryAttackEnded = false;
                terrainHitCount = 0;
            }
        }

        private void ResetSecondaryAttack()
        {
            var hitTime = WeaponUtils.GetAttackDuration(secondaryAttack);
            secondaryAttackTimer = Mathf.Min(hitTime / 2, 0.3f);
            secondaryAttackTimerFull = -hitTime + secondaryAttackTimer;
            firstPos = Vector3.zero;
            lastPos = Vector3.zero;
            isSecondaryAttackStarted = false;
            slashTrail.emitting = false;
            lastpointList = new List<Vector3>();
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
                //Follows Attack.DoMeleeAttack Function 
                GameObject hitObject = Projectile.FindHitObject(raycastHit.collider);
                if (hitObject && hitObject != Player.m_localPlayer.gameObject)
                {
                    Vagon vagon = hitObject.GetComponent<Vagon>();
                    if (!vagon || !vagon.IsAttached(Player.m_localPlayer))
                    {
                        Character targetCharacter = hitObject.GetComponent<Character>();
                        if (targetCharacter != null)
                        {
                            bool flag = BaseAI.IsEnemy(Player.m_localPlayer, targetCharacter) || (targetCharacter.GetBaseAI() && targetCharacter.GetBaseAI().IsAggravatable());
                            if ((!item.m_shared.m_tamedOnly && !Player.m_localPlayer.IsPVPEnabled() && !flag) || (item.m_shared.m_tamedOnly && !targetCharacter.IsTamed()))
                            {
                                continue;
                            }
                            if (item.m_shared.m_dodgeable && targetCharacter.IsDodgeInvincible())
                            {
                                continue;
                            }
                        }
                        else if (item.m_shared.m_tamedOnly)
                        {
                            continue;
                        }
                        bool multiCollider = secondaryAttack.m_pickaxeSpecial && (hitObject.GetComponent<MineRock5>() || hitObject.GetComponent<MineRock>());
                        secondaryAttack.AddHitPoint(secondaryHitList, hitObject, raycastHit.collider, raycastHit.point, raycastHit.distance, multiCollider);
                        if (!secondaryAttack.m_hitThroughWalls && Vector3.Distance(raycastList[0].point, raycastHit.point) >= 0.3f && attack.m_attackAnimation != "swing_pickaxe")
                        {
                            break;
                        }
                    }
                }
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
