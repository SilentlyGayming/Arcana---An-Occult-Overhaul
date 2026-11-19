using System;
using System.Collections.Generic;
using System.Reflection;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.EventSystem;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.Gameplay.Utilities;
using Sims3.Gameplay;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    internal static class WitchCastingLifecycle
    {
        private static readonly FieldInfo SkillMultipliers = typeof(InteractionInstance).GetField("mSkillMultipliers",
            BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Dictionary<ulong, WeakReference> practice = new Dictionary<ulong, WeakReference>();
        private static EventListener instantiated;
        private static EventListener practiced;
        private static AlarmHandle recovery = AlarmHandle.kInvalidHandle;

        internal static bool CastingInstalled { get { return SpellKnowledgeGates.Installed; } }

        internal static void Prepare()
        {
            if (GameUtils.IsInstalled(ProductVersion.EP7))
            {
                WitchMagicRecovery.RegisterFizzle();
            }
        }

        internal static void Start()
        {
            // Installs the knowledge gates, event listeners and regeneration alarm for Witch casting.
            if (!GameUtils.IsInstalled(ProductVersion.EP7))
            {
                return;
            }

            WitchMagicRecovery.RegisterFizzle();
            SpellKnowledgeGates.Prepare();

            instantiated = EventTracker.AddListener(EventTypeId.kSimInstantiated, OnInstantiated);
            practiced = EventTracker.AddListener(EventTypeId.kPracticedSpellcasting, OnPractice);
            foreach (Sims3.Gameplay.Abstracts.GameObject target in Sims3.Gameplay.Queries.GetObjects<Sims3.Gameplay.Abstracts.GameObject>())
            {
                SpellKnowledgeGates.InstallFor(target);
            }
            WitchMagicRecovery.RefreshAll();
            recovery = AlarmManager.Global.AddAlarmRepeating(5f, TimeUnit.Minutes,
                WitchMagicRecovery.RefreshAll, 5f, TimeUnit.Minutes, "Arcana Witch regeneration", AlarmType.NeverPersisted, null);
        }

        internal static void Stop()
        {
            // Removes the casting listeners and alarm and restores native regeneration.
            if (instantiated != null)
            {
                EventTracker.RemoveListener(instantiated);
                instantiated = null;
            }
            if (practiced != null)
            {
                EventTracker.RemoveListener(practiced);
                practiced = null;
            }
            if (recovery != AlarmHandle.kInvalidHandle)
            {
                AlarmManager manager = AlarmManager.Global;
                if (manager != null)
                {
                    manager.RemoveAlarm(recovery);
                }
                recovery = AlarmHandle.kInvalidHandle;
            }
            WitchMagicRecovery.RestoreAll();
            practice.Clear();
        }

        private static ListenerAction OnInstantiated(Event e)
        {
            Sim sim = e.Actor as Sim ?? e.TargetObject as Sim;
            if (sim != null)
            {
                JournalBootstrap.RunLater(delegate { InstallFor(sim); });
            }
            return ListenerAction.Keep;
        }

        private static void InstallFor(Sim sim)
        {
            SpellKnowledgeGates.InstallFor(sim);
        }

        private static ListenerAction OnPractice(Event e)
        {
            // Applies the Practice Magic experience bonus once per practice interaction.
            Sim sim = e.Actor as Sim;
            if (!WitchCastingContext.IsWitch(sim) || SkillMultipliers == null)
            {
                return ListenerAction.Keep;
            }

            MagicWand.PracticeSpellCasting interaction = sim.CurrentInteraction as MagicWand.PracticeSpellCasting;
            if (interaction == null || !interaction.HasCommodityUpdate(CommodityKind.SkillSpellcasting))
            {
                return ListenerAction.Keep;
            }

            ulong id = sim.SimDescription.SimDescriptionId;
            WeakReference previous;
            if (practice.TryGetValue(id, out previous) && previous.Target == interaction)
            {
                return ListenerAction.Keep;
            }

            Dictionary<SkillNames, float> multipliers = SkillMultipliers.GetValue(interaction) as Dictionary<SkillNames, float>;
            float current;
            if (multipliers != null && multipliers.TryGetValue(SkillNames.Spellcasting, out current))
            {
                interaction.AlterSkillUpdateMultiplier(CommodityKind.SkillSpellcasting, current * (WitchCastingRules.PracticeExperience - 1f));
                practice[id] = new WeakReference(interaction);
            }
            return ListenerAction.Keep;
        }
    }
}
