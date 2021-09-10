﻿using BepInEx;
using R2API.Utils;
using System.Security;
using System.Security.Permissions;
using UnityEngine;
using R2API;
using RoR2;
using TMPro;
using RoR2.UI;
using R2API.Networking;
using UnityEngine.Networking;
using BepInEx.Configuration;

using System;

[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace DeathMessageAboveCorpse
{
    [BepInPlugin("com.DestroyedClone.DeathMessageAboveCorpse", "Death Message Above Corpse", "1.0.0")]
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.NoNeedForSync, VersionStrictness.DifferentModVersionsAreOk)]
    [R2APISubmoduleDependency(nameof(EffectAPI), nameof(PrefabAPI), nameof(NetworkingAPI))]
    public class DeathMessageAboveCorpsePlugin : BaseUnityPlugin
    {
        public static string[] deathMessages = new string[]
        {
            "shak pls.",
            "You are dead.",
            "You embrace the void.",
            "You had a lot more to live for.",
            "Your internal organs have failed.",
            "Your body was gone an hour later.",
            "Your family will never know how you died.",
            "You died painlessly.",
            "Your death was extremely painful.",
            "You have broken every bone in your body.",
            "You die a slightly embarrassing death.",
            "You die in a hilarious pose.",
            "You really messed up.",
            "You have died. Maybe next time..",
            "You have passed away. Try again?",
            "Choose a new character?",
            "Remember to activate use items.",
            "Remember that as time increases, so does difficulty.",
            "This planet has killed you.",
            "It wasn't your time to die...",
            "That was definitely not your fault.",
            "That was absolutely your fault.",
            "They will surely feast on your flesh.",
            "..the harder they fall.",
            "Beep.. beep.. beeeeeeeeeeeeeeeee",
            "Close!",
            "Come back soon!",
            "Crushed.",
            "Smashed.",
            "DEAD",
            "Get styled upon.",
            "Dead from blunt trauma to the face.",
            "ded",
            "rekt",
            "ur dead LOL get rekt",
            "Sucks to suck.",
            "You walk towards the light.",

            // TODO: Seperate based on difficulty (Run.instance.selectedDifficulty)
            "Try playing on \"Drizzle\" mode for an easier time.",
            "Consider lowering the difficulty.",
        };

        public static ConfigEntry<float> cfgDuration;
        public static ConfigEntry<bool> cfgFinalSurvivorCorpseKept;

        public static GameObject defaultTextObject;

        // Text displays larger for the client in the middle of the screen (https://youtu.be/vQRPpSx5WLA?t=1336)
        // 3 second delay after the corpse is on the ground before showing either client or server message
        //

        public void Awake()
        {
            SetupConfig();
            On.RoR2.GlobalEventManager.OnPlayerCharacterDeath += GlobalEventManager_OnPlayerCharacterDeath;
            if (cfgFinalSurvivorCorpseKept.Value)
                On.RoR2.ModelLocator.OnDestroy += ModelLocator_OnDestroy;
            defaultTextObject = CreateDefaultTextObject();
        }

        private void ModelLocator_OnDestroy(On.RoR2.ModelLocator.orig_OnDestroy orig, ModelLocator self)
        {
            //self.characterMotor.body.master.IsDeadAndOutOfLivesServer()
            if (self?.characterMotor?.body?.master && self.characterMotor.body.healthComponent && !self.characterMotor.body.healthComponent.alive)
            {
                self.preserveModel = true;
                self.noCorpse = true;
            }
            orig(self);
        }

        public void SetupConfig()
        {
            cfgDuration = Config.Bind("", "Duration", 60f, "Length of time in seconds the message stays out.");
            cfgFinalSurvivorCorpseKept = Config.Bind("", "Keep Final Corpse Alive", true, "If true, keeps the player's final/last-life corpse from getting deleted until the message is finished.");
        }

        private void GlobalEventManager_OnPlayerCharacterDeath(On.RoR2.GlobalEventManager.orig_OnPlayerCharacterDeath orig, RoR2.GlobalEventManager self, RoR2.DamageReport damageReport, RoR2.NetworkUser victimNetworkUser)
        {
            orig(self, damageReport, victimNetworkUser);

            var deathMessage = GetDeathMessage();
            var textObject = UnityEngine.Object.Instantiate<GameObject>(defaultTextObject);
            textObject.transform.position = damageReport.victimBody.corePosition + Vector3.up * 2f;
            ShowDeathMessageComponent showDeathMessageComponent = textObject.GetComponent<ShowDeathMessageComponent>();
            showDeathMessageComponent.textMeshPro.text = deathMessage;
            //showDeathMessageComponent.textMeshPro.font = Resources.Load<TMP_FontAsset>("tmpfonts/fontsource/RiskofRainFont");
            //showDeathMessageComponent.textMeshPro.fontMaterial = Resources.Load<Material>("tmpfonts/fontsource/riskofrainfont");
            showDeathMessageComponent.transformToWatch = damageReport.victim.modelLocator.modelTransform.transform;
            showDeathMessageComponent.languageTextMeshController.token = deathMessage;
            NetworkServer.Spawn(textObject);
        }

        public string GetDeathMessage()
        {
            return deathMessages[UnityEngine.Random.Range(0, deathMessages.Length)];
        }

        public static GameObject CreateDefaultTextObject()
        {
            var textPrefab = PrefabAPI.InstantiateClone(Resources.Load<GameObject>("prefabs/effects/DamageRejected"), "DeathMessageAboveCorpse_DefaultTextObjectChild");
            textPrefab.name = "DeathMessageAboveCorpse_DefaultTextObject";
            UnityEngine.Object.Destroy(textPrefab.GetComponent<EffectComponent>());
            textPrefab.GetComponent<ObjectScaleCurve>().overallCurve = AnimationCurve.Constant(0f, 1f, 1f);
            UnityEngine.Object.Destroy(textPrefab.GetComponent<VelocityRandomOnStart>());
            UnityEngine.Object.Destroy(textPrefab.GetComponent<ConstantForce>());
            UnityEngine.Object.Destroy(textPrefab.GetComponent<Rigidbody>());
            textPrefab.AddComponent<NetworkIdentity>();
            ShowDeathMessageComponent showDeathMessageComponent = textPrefab.AddComponent<ShowDeathMessageComponent>();
            showDeathMessageComponent.duration = 3f;
            showDeathMessageComponent.gameObjectToEnable = textPrefab.transform.Find("TextMeshPro").gameObject;
            showDeathMessageComponent.textMeshPro = showDeathMessageComponent.gameObjectToEnable.GetComponent<TextMeshPro>();
            showDeathMessageComponent.textMeshPro.fontSize = 2f;
            showDeathMessageComponent.languageTextMeshController = showDeathMessageComponent.gameObjectToEnable.GetComponent<LanguageTextMeshController>();


            if (cfgDuration.Value > 0)
            {
                showDeathMessageComponent.destroyOnTimer = textPrefab.GetComponent<DestroyOnTimer>();
                showDeathMessageComponent.destroyOnTimer.duration = cfgDuration.Value;
            }
            else
            {
                UnityEngine.Object.Destroy(textPrefab.GetComponent<DestroyOnTimer>());
            }

            if (textPrefab) { PrefabAPI.RegisterNetworkPrefab(textPrefab); }
            return textPrefab;
        }

        public class ShowDeathMessageComponent : MonoBehaviour
        {
            public DestroyOnTimer destroyOnTimer;
            public GameObject gameObjectToEnable;

            private float age;
            public float duration;
            public bool stoppedMoving;

            public TextMeshPro textMeshPro;
            public LanguageTextMeshController languageTextMeshController;

            public Transform transformToWatch;

            public Vector3 lastPosition = Vector3.zero;

            public CameraRigController cameraRig;
            public GameObject target;

            private float lenience = 0.1f;

            private void Awake()
            {
                cameraRig = CameraRigController.readOnlyInstancesList[0];
                if (cameraRig) target = cameraRig.target;
            }

            private void Start()
            {
                if (destroyOnTimer) destroyOnTimer.enabled = false;
                gameObjectToEnable.SetActive(false);
            }

            private void FixedUpdate()
            {
                if (cameraRig && cameraRig.targetParams && cameraRig.targetParams.cameraPivotTransform && cameraRig.target == target)
                {
                    lastPosition = cameraRig.targetParams.cameraPivotTransform.position;
                    /*EffectManager.SpawnEffect(Resources.Load<GameObject>("prefabs/effects/DamageRejected"), new EffectData()
                    {
                        origin = lastPosition
                    }, true);*/
                    if (Mathf.Abs(Vector3.Distance(cameraRig.targetParams.cameraPivotTransform.position, lastPosition)) > lenience)
                    {
                        this.age = 0f;
                        return;
                    }
                }
                this.age += Time.fixedDeltaTime;

                if (this.age > this.duration)
                {
                    //bool grounded = false;
                    Physics.Raycast(lastPosition, Vector3.down, out RaycastHit raycastHit, 1000f, LayerIndex.world.mask);
                    if (Vector3.Distance(raycastHit.point, lastPosition) >= 3f || !transformToWatch)
                    {
                        //grounded = true;
                        lastPosition = raycastHit.point;
                    }
                    gameObject.transform.position = lastPosition + Vector3.up * 3f;
                    if (destroyOnTimer) destroyOnTimer.enabled = true;
                    if (gameObjectToEnable) gameObjectToEnable.SetActive(true);
                    if (transformToWatch && !transform.gameObject.GetComponent<Corpse>()) transformToWatch.gameObject.AddComponent<Corpse>();
                    enabled = false;
                }
            }
        }
    }
}