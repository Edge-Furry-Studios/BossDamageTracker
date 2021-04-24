﻿using BepInEx;
using BepInEx.Configuration;
using UnityEngine;
using RoR2;
using R2API.Utils;
using static RoR2.EquipmentIndex;
using System.Collections.ObjectModel;
using UnityEngine.Networking;
using RoR2.CharacterAI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using EntityStates;
using JetBrains.Annotations;
using RoR2.Navigation;
using UnityEngine.AI;
using EntityStates.GoldGat;
using System.Security;
using System.Security.Permissions;

using EntityStates.AI;
using static RoR2.RoR2Content;


[module: UnverifiableCode]
#pragma warning disable CS0618 // Type or member is obsolete
[assembly: SecurityPermission(SecurityAction.RequestMinimum, SkipVerification = true)]
#pragma warning restore CS0618 // Type or member is obsolete

namespace MonstersGrabItems
{
    [BepInPlugin("com.DestroyedClone.MonstersGrabItems", "Monsters Grab Items", "1.0.0")]
    [BepInDependency(R2API.R2API.PluginGUID, R2API.R2API.PluginVersion)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.EveryoneNeedSameModVersion)]
    public class MGI_Plugin : BaseUnityPlugin
    {
		public static EquipmentIndex[] desirableEquipment = { };

        void Awake()
        {
            On.RoR2.GenericPickupController.BodyHasPickupPermission += GenericPickupController_BodyHasPickupPermission;
            On.RoR2.GenericPickupController.AttemptGrant += GenericPickupController_AttemptGrant;
            On.RoR2.GenericPickupController.GrantLunarCoin += GenericPickupController_GrantLunarCoin;
            On.RoR2.GenericPickupController.GrantItem += GenericPickupController_GrantItem;
        }

        private void GenericPickupController_GrantItem(On.RoR2.GenericPickupController.orig_GrantItem orig, GenericPickupController self, CharacterBody body, Inventory inventory)
        {
			if (!NetworkServer.active)
			{
				Debug.LogWarning("[Server] function 'System.Void RoR2.GenericPickupController::GrantItem(RoR2.CharacterBody,RoR2.Inventory)' called on client");
				return;
			}
			PickupDef pickupDef = PickupCatalog.GetPickupDef(self.pickupIndex);
			var itemIndex = (pickupDef != null) ? pickupDef.itemIndex : ItemIndex.None;
			inventory.GiveItem(itemIndex, 1);
			GenericPickupController.SendPickupMessage(inventory.GetComponent<CharacterMaster>(), self.pickupIndex);
			body.master?.GetBodyObject().GetComponent<DropInventoryOnDeath>()?.AddItem(itemIndex, 1);
			UnityEngine.Object.Destroy(self.gameObject);
		}

        private void GenericPickupController_GrantLunarCoin(On.RoR2.GenericPickupController.orig_GrantLunarCoin orig, GenericPickupController self, CharacterBody body, uint count)
        {
			if (!NetworkServer.active)
			{
				Debug.LogWarning("[Server] function 'System.Void RoR2.GenericPickupController::GrantLunarCoin(RoR2.CharacterBody,System.UInt32)' called on client");
				return;
			}
			CharacterMaster master = body.master;
			NetworkUser networkUser = Util.LookUpBodyNetworkUser(body);
			if (networkUser)
			{
				if (master)
				{
					GenericPickupController.SendPickupMessage(master, self.pickupIndex);
				}
				networkUser.AwardLunarCoins(count);
				UnityEngine.Object.Destroy(self.gameObject);
			}
			else
            {
				if (master)
                {
					if (master.GetBodyObject().GetComponent<DropInventoryOnDeath>())
                    {
						master.GetBodyObject().GetComponent<DropInventoryOnDeath>().incrementCoins();
						GenericPickupController.SendPickupMessage(master, self.pickupIndex);
						UnityEngine.Object.Destroy(self.gameObject);
					}
                }
            }
		}

        private void GenericPickupController_AttemptGrant(On.RoR2.GenericPickupController.orig_AttemptGrant orig, GenericPickupController self, CharacterBody body)
        {
			if (!NetworkServer.active)
			{
				Debug.LogWarning("[Server] function 'System.Void RoR2.GenericPickupController::AttemptGrant(RoR2.CharacterBody)' called on client");
				return;
			}

			TeamComponent component = body.GetComponent<TeamComponent>();
			if (component)
			{
				Inventory inventory = body.inventory;
				if (inventory)
				{
					PlayerCharacterMasterController isPlayer = body.masterObject.GetComponent<PlayerCharacterMasterController>();

					self.consumed = true;
					PickupDef pickupDef = PickupCatalog.GetPickupDef(self.pickupIndex);
					DropInventoryOnDeath comp = null;
					if (!isPlayer)
                    {
						comp = body.gameObject.GetComponent<DropInventoryOnDeath>();
						if (!comp)
							comp = body.gameObject.AddComponent<DropInventoryOnDeath>();
					}
					if (pickupDef.itemIndex != ItemIndex.None)
					{
						self.GrantItem(body, inventory);
					}
					if (pickupDef.coinValue != 0U)
					{
						self.GrantLunarCoin(body, pickupDef.coinValue);
					}
					if (isPlayer)
					{
						if (pickupDef.equipmentIndex != EquipmentIndex.None)
						{
							self.GrantEquipment(body, inventory);
						}
						if (pickupDef.artifactIndex != ArtifactIndex.None)
						{
							self.GrantArtifact(body, pickupDef.artifactIndex);
						}
					}
				}
			}
		}

        private bool GenericPickupController_BodyHasPickupPermission(On.RoR2.GenericPickupController.orig_BodyHasPickupPermission orig, CharacterBody body)
        {
            return body.masterObject && body.inventory;
        }

		public class DropInventoryOnDeath : MonoBehaviour, IOnKilledServerReceiver
        {
			List<ItemIndex> itemIndices = new List<ItemIndex>();
			EquipmentIndex equipmentIndex = EquipmentIndex.None;
			uint coinCount = 0U;

			public void AddItem(ItemIndex itemIndex, uint amount)
            {
				for (int i = 0; i < amount; i++)
                {
					itemIndices.Add(itemIndex);
                }
            }

			public void incrementCoins()
            {
				coinCount++;
            }

			public void OnKilledServer(DamageReport damageReport)
			{
				Vector3 position = Vector3.zero;
				if (damageReport.attackerBody)
                {
					position = damageReport.victimBody.corePosition;
                } else
				{
					foreach (var teammate in TeamComponent.GetTeamMembers(TeamIndex.Player))
                    {
						if (teammate.body?.master?.playerCharacterMasterController)
                        {
							position = teammate.body.corePosition;
                        }
                    }
				}

				DropItems(position);

				PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(equipmentIndex), position, Vector3.up * 20f);

				DropCoins(position);
			}

			public void DropItems(Vector3 position)
            {
				if (itemIndices.Count == 0) return;
				float angle = 360f / itemIndices.Count;
				var chestVelocity = (Vector3.up * 20f) + (Vector3.forward * 5f);
				Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
				int i = 0;
				while (i < itemIndices.Count)
				{
					PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex(itemIndices[i]), position + Vector3.up * 1.5f, chestVelocity);
					i++;
					chestVelocity = rotation * chestVelocity;
				}
			}

			public void DropCoins(Vector3 position)
            {
				if (coinCount == 0) return;
				float angle = 360f / coinCount;
				var chestVelocity = (Vector3.up * 20f) + (Vector3.forward * 5f);
				Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
				int i = 0;
				while (i < coinCount)
				{
					PickupDropletController.CreatePickupDroplet(PickupCatalog.FindPickupIndex("LunarCoin.Coin0"), position + Vector3.up * 1.5f, chestVelocity);
					i++;
					chestVelocity = rotation * chestVelocity;
				}
			}
        }
    }
}
