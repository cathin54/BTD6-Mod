﻿using System;
using System.Collections.Generic;
using Assets.Scripts.Models;
using Assets.Scripts.Models.Towers;
using Assets.Scripts.Models.Towers.Behaviors.Abilities;
using Assets.Scripts.Simulation.Towers;
using Assets.Scripts.Unity.UI_New.InGame;
using Assets.Scripts.Unity.UI_New.Main;
using Assets.Scripts.Unity.UI_New.Popups;
using BloonsTD6_Mod_Helper.Extensions;
using Harmony;
using Assets.Scripts.Models.Profile;
using Assets.Scripts.Models.Towers.Behaviors;
using Assets.Scripts.Models.Towers.Behaviors.Abilities.Behaviors;
using Assets.Scripts.Unity;
using MelonLoader;

[assembly: MelonInfo(typeof(AbilityChoice.Main), "Ability Choice", "1.0.2", "doombubbles")]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace AbilityChoice
{
    public class Main : MelonMod
    {
        public static HashSet<int> CurrentTowerIDs = new HashSet<int>();
        public static Dictionary<int, int> CurrentBoostIDs = new Dictionary<int, int>();

        private static readonly Dictionary<string, string> AllUpgrades = new Dictionary<string, string>
        {
            {"Plasma Monkey Fan Club", "Permanently shoots powerful plasma blasts itself."},
            {"Super Monkey Fan Club", "Gains permanent Super Attack speed and range itself."},
            {"Perma Charge", "Smaller but constant damage buff."},
            {"Turbo Charge", "Smaller but constant attack speed buff."},
            {"MOAB Eliminator", "Does extremely further increased MOAB damage."},
            {"MOAB Assassin", "Does even further increased MOAB damage."},
            {"Bomb Blitz", "More explosion damage."},
            {"Super Maelstrom", "More range, pierce, and lifespan."},
            {"Blade Maelstrom", "More range and pierce; blades seek out Bloons."},
            {"Absolute Zero", "Further slow, buffs nearby Ice Monkey always."},
            {"Snowstorm", "Partially slows MOAB class bloons."},
            {"Glue Storm", "Main attacks apply weakening, slower ability glue."},
            {"Glue Strike", "Main attacks apply weakening ability glue."},
            {"Pre-emptive Strike", "Gains even frequenter First Strike style Missile attacks."},
            {"First Strike Capability", "Gains frequent First Strike style Missile attacks."},
            {"Buccaneer-Pirate Lord", "All attacks do significantly increased MOAB and Ceramic damage."},
            {"Buccaneer-Monkey Pirates", "Cannon attacks do significantly increased MOAB and Ceramic  damage."},
            {"M.A.D", "Shoots Rocket Storm missiles alongside its main attack."},
            {"Rocket Storm", "Shoots Rocket Storm missiles alongside its main attack."},
            {"Pop and Awe", "Occasionally causes mini-Pop and Awe effects on target."},
            {"Elite Sniper", "Always gives one crate at the start of each round."},
            {"Supply Drop", "Always gives one crate at the start of each round."},
            {"Special Poperations", "Previous effects, and Marine permanently attacks from inside the heli."},
            {"Support Chinook", "Always gives one crate at the start of each round (Move ability unchanged)."},
            {"Tsar Bomba", "Occasionally drops mini Tsar Bombs."},
            {"Ground Zero", "Occasionally drops mini Ground Zeros."},
            {"Wizard Lord Phoenix", "Super Phoenix is also permanent but weaker."},
            {"Summon Phoenix", "Phoenix is permanent but weaker."},
            {"The Anti-Bloon", "Frequently does a weaker version of Annihilation ability."},
            {"Legend of the Night", "Increased range."},
            {"Dark Champion", "Increased range."},
            {"Dark Knight", "Increased range."},
            {"Tech Terror", "Frequently does a weaker version of Annihilation ability."},
            {"Grand Saboteur", "Ninja's attack do multiplicatively increased damage to MOAB class Bloons."},
            {"Bloon Sabotage", "Ninja’s attacks have more range and slow down Bloons themselves."},
            {"Spirit of the Forest", "Also 25 lives per round."},
            {"Jungle's Bounty", "$200 per round, nearby income increased by 15%."},
            {"Total Transformation", "Has permanent extra strength transformation laser attack on self only."},
            {"Transforming Tonic", "Has permanent but weaker transformation laser attack."},
            {"Monkey-Nomics", "No longer has a maximum capacity."},
            {"IMF loan", "Maximum capacity is $14,000 (by default)."},
            {"Homeland Defense", "Permanent global attack speed buff."},
            {"Call to Arms", "Permanent weaker nearby attack speed buff."},
            {"Ultraboost", "Modified Ability: Permanently boost (based on tier) one tower at a time."},
            {"Overclock", "Modified Ability: Permanently boost (based on tier) one tower at a time."},
            {"Carpet of Spikes", "Launchs a continuous stream of spikes across the track."},
            {"Spike Storm", "Launchs a continuous stream of spikes across the track."}
        };

        [HarmonyPatch(typeof(MainMenu), nameof(MainMenu.OnEnable))]
        internal class MainMenu_OnEnable
        {
            [HarmonyPostfix]
            internal static void Postfix()
            {
                CurrentTowerIDs = new HashSet<int>();
                CurrentBoostIDs = new Dictionary<int, int>();
            }
        }

        [HarmonyPatch(typeof(TowerManager), nameof(TowerManager.UpgradeTower))]
        internal class TowerManager_UpgradeTower
        {
            [HarmonyPrefix]
            internal static bool Prefix(Tower tower, TowerModel def, ref string __state)
            {
                __state = "";
                foreach (var upgrade in AllUpgrades.Keys)
                {
                    if (!tower.towerModel.appliedUpgrades.Contains(upgrade) && def.appliedUpgrades.Contains(upgrade))
                    {
                        __state = upgrade;
                    }
                }

                return true;
            }

            [HarmonyPostfix]
            internal static void Postfix(Tower tower, string __state)
            {
                if (__state != "")
                {
                    PopupScreen.instance.ShowPopup(PopupScreen.Placement.inGameCenter,
                        "Ability Choice (Can't be Undone)",
                        $"Do you want to forego the {__state} ability to instead get \"{AllUpgrades[__state]}\"",
                        new Action(() => { EnableForTower(tower); }), "Yes",
                        new Action(() => { DisableForTower(tower); }), "No", Popup.TransitionAnim.Scale
                    );
                }
                else if (CurrentTowerIDs.Contains(tower.Id))
                {
                    EnableForTower(tower);
                }
            }
        }

        public static void EnableForTower(Tower tower)
        {
            CurrentTowerIDs.Add(tower.Id);

            var removeAbility = true;

            TowerModel towerModel = Game.instance.model.GetTower(tower.towerModel.baseId, 
                tower.towerModel.tiers[0], tower.towerModel.tiers[1], tower.towerModel.tiers[2]).Duplicate();
            
            //don't ask
            towerModel.RemoveBehaviors<AbilityModel>();
            foreach (var abilityModel in tower.towerModel.GetAbilites())
            {
                towerModel.AddBehavior(abilityModel);
            }

            foreach (var upgrade in AllUpgrades.Keys)
            {
                if (tower.towerModel.appliedUpgrades.Contains(upgrade))
                {
                    if (upgrade == "Summon Phoenix")
                    {
                        foreach (var t2s in InGame.instance.UnityToSimulation.GetAllTowers())
                        {
                            if (t2s.tower.parentTowerId == tower.Id)
                            {
                                t2s.tower.Destroy();
                                break;
                            }
                        }
                    }
                    if (upgrade == "Wizard Lord Phoenix")
                    {
                        foreach (var t2s in InGame.instance.UnityToSimulation.GetAllTowers())
                        {
                            if (t2s.tower.parentTowerId == tower.Id)
                            {
                                if (t2s.tower.towerModel.baseId == "LordPhoenix")
                                {
                                    t2s.tower.Destroy();
                                }
                                else
                                {
                                    var lord = Game.instance.model.GetTower(TowerType.WizardMonkey, tower.towerModel.tiers[0], 5, tower.towerModel.tiers[2]);
                                    var phoenix = lord.GetBehavior<TowerCreateTowerModel>().towerModel;
                                    t2s.tower.SetTowerModel(phoenix);
                                }
                            }
                        }
                    }
                    
                    var methodName = upgrade.Replace(" ", "").Replace("'", "")
                        .Replace("-", "").Replace(".", "");
                    
                    var methodInfo = typeof(Towers).GetMethod(methodName);
                    if (methodInfo == null)
                    {
                        MelonLogger.Log("Couldn't find method " + methodName);
                    }
                    else
                    {
                        methodInfo.Invoke(null, new object[] {towerModel});
                    }

                    if (upgrade == "Supply Drop" || upgrade == "Elite Sniper" || upgrade == "Carpet of Spikes" ||
                        upgrade == "Support Chinook" || upgrade == "Special Poperations" || upgrade == "Overclock" ||
                        upgrade == "Ultraboost")
                    {
                        removeAbility = false;
                    }

                    break;
                }
            }

            if (removeAbility)
            {
                towerModel.behaviors = towerModel.behaviors.RemoveItemOfType<Model, AbilityModel>();
            }

            tower.SetTowerModel(towerModel);

            InGame.instance.bridge.OnAbilitiesChangedSim();
        }

        public static void DisableForTower(Tower tower)
        {
            CurrentTowerIDs.Remove(tower.Id);
            InGame.instance.bridge.OnAbilitiesChangedSim();
            if (CurrentBoostIDs.ContainsKey(tower.Id))
            {
                Overclock.RemoveBoostOn(CurrentBoostIDs[tower.Id]);
                CurrentBoostIDs.Remove(tower.Id);
            }
        }
        
        
        [HarmonyPatch(typeof(Tower), nameof(Tower.SetSaveData))]
        public class Tower_SetSaveData
        {
            [HarmonyPostfix]
            public static void Postfix(Tower __instance, TowerSaveDataModel towerData)
            {
                if (towerData.metaData.ContainsKey("AbilityChoice"))
                {
                    EnableForTower(__instance);
                }

                if (towerData.metaData.ContainsKey("AbilityChoiceBoosting"))
                {
                    var id = int.Parse(towerData.metaData["AbilityChoiceBoosting"]);
                    var tower = InGame.instance.GetTowerManager().GetTowerByIdLastSave(id);
                    CurrentBoostIDs[__instance.Id] = tower.Id;
                }

                if (towerData.metaData.ContainsKey("AbilityChoiceStacks"))
                {
                    int stacks = int.Parse(towerData.metaData["AbilityChoiceStacks"]);
                    Overclock.UltraBoostFixes[__instance] = stacks;
                }

                if (towerData.metaData.ContainsKey("AbilityChoicePhoenix"))
                {
                    var phoenix = __instance.towerModel.Duplicate();
                    
                    phoenix.behaviors = phoenix.behaviors.RemoveItemOfType<Model, TowerExpireModel>();
                    foreach (var weaponModel in phoenix.GetWeapons())
                    {
                        weaponModel.rate *= 3f;
                    }
                    
                    __instance.SetTowerModel(phoenix);
                }
            }
        }

        [HarmonyPatch(typeof(Tower), nameof(Tower.GetSaveData))]
        public class Tower_GetSaveData
        {
            [HarmonyPostfix]
            public static void Postfix(ref TowerSaveDataModel __result, Tower __instance)
            {
                if (CurrentTowerIDs.Contains(__instance.Id))
                {
                    __result.metaData["AbilityChoice"] = "Yup";
                }

                if (CurrentBoostIDs.ContainsKey(__instance.Id))
                {
                    __result.metaData["AbilityChoiceBoosting"] = "" + CurrentBoostIDs[__instance.Id];
                }
                
                var mutator = __instance.GetMutatorById("Ultraboost");
                if (mutator != null)
                {
                    var stacks = mutator.mutator.Cast<OverclockPermanentModel.OverclockPermanentMutator>().stacks;
                    __result.metaData["AbilityChoiceStacks"] = "" + stacks;
                }

                if (__instance.towerModel.baseId == "PermaPhoenix" || __instance.towerModel.baseId == "LordPhoenix")
                {
                    var id = __instance.parentTowerId;
                    var tower = InGame.instance.GetTowerManager().GetTowerById(id);
                    if (CurrentTowerIDs.Contains(id) && (__instance.towerModel.baseId == "LordPhoenix" || tower.towerModel.tier < 5))
                    {
                        __result.metaData["AbilityChoicePhoenix"] = "Yup";
                    }
                }
            }
        }
    }
}