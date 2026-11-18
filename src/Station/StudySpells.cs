using System.Reflection;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.Autonomy;
using Sims3.Gameplay.Interactions;
using Sims3.Gameplay.Objects.Alchemy;
using Sims3.Gameplay.Skills;
using Sims3.SimIFace;

namespace SupernaturalExpanded.Journal
{
    // Studies at the station with the native book animation and no Alchemy side effects: it trains only the occult's own skill.
    public sealed class StudySpells : Interaction<Sim, AlchemyStation>
    {
        private const uint BenchSlot = 824351309U;
        private const uint PageOffEvent = 101;
        private const uint PageOnEvent = 102;

        private const BindingFlags Methods = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private static readonly MethodInfo StartCandles = typeof(AlchemyStation).GetMethod("StartLitCandlesVFX", Methods);
        private static readonly MethodInfo StopCandles = typeof(AlchemyStation).GetMethod("StopLitCandlesVFX", Methods);
        private static readonly MethodInfo Extinguish = typeof(AlchemyStation).GetMethod("FireOneShotCandleExtinguishVFX", Methods);

        public static readonly InteractionDefinition Singleton = new Definition();

        [Persistable(false)]
        private Skill studied;
        private SkillNames studiedName;
        private float appliedRate;

        protected override bool Run()
        {
            SpellStudyTrack track = SpellStudy.TrackFor(Actor);
            if (track == null)
            {
                return false;
            }

            studiedName = track.Skill;
            Actor.SkillManager.AddElement(track.Skill);
            studied = Actor.SkillManager.GetElement(track.Skill);
            if (studied == null || !Actor.RouteToSlot(Target, (Slot)BenchSlot))
            {
                return false;
            }

            StandardEntry();
            try
            {
                AcquireStateMachine("AlchemyStation");
                SetActor("x", Actor);
                SetActor("alchemyStation", Target);
                SetParameter("ProductVersion", ProductVersion.EP7);
                EnterState("x", "EnterAlchemyBench");
                AddPersistentScriptEventHandler(PageOffEvent, HidePage);
                AddPersistentScriptEventHandler(PageOnEvent, ShowPage);
                Candles(StartCandles);
                AnimateSim("ResearchingAlchemy");
                BeginStudy();
                bool finished = DoLoop(ExitReason.Default | ExitReason.MaxSkillPointsReached, new InsideLoopFunction(StudyLoop), mCurrentStateMachine);
                EndStudy();
                AnimateSim("ExitResearch");
                Candles(StopCandles);
                Candles(Extinguish);
                return finished;
            }
            finally
            {
                EndStudy();
                StandardExit();
            }
        }

        public override void Cleanup()
        {
            try
            {
                EndStudy();
                if (Target != null)
                {
                    Candles(StopCandles);
                }
            }
            finally
            {
                base.Cleanup();
            }
        }

        // The station's candle effects are not public in every build of the game, so they are called by name and skipped if absent.
        private void Candles(MethodInfo effect)
        {
            if (effect != null)
            {
                effect.Invoke(Target, null);
            }
        }

        private void BeginStudy()
        {
            appliedRate = SpellStudy.SkillPointsPerSimMinute;
            Actor.SkillManager.StartSkillGain(studiedName, appliedRate);
        }

        // Safe to call more than once: only the rate this interaction added is taken back, so other gains on the skill are untouched.
        private void EndStudy()
        {
            if (appliedRate <= 0f)
            {
                return;
            }

            float rate = appliedRate;
            appliedRate = 0f;
            if (Actor != null && Actor.SkillManager != null)
            {
                Actor.SkillManager.StopSkillGain(studiedName, rate);
            }
        }

        private void StudyLoop(StateMachineClient client, LoopData loop)
        {
            if (studied != null && studied.ReachedMaxLevel())
            {
                Actor.AddExitReason(ExitReason.MaxSkillPointsReached);
            }
        }

        private void HidePage(StateMachineClient client, IEvent evt)
        {
            Target.SetGeometryState("pageOff");
        }

        private void ShowPage(StateMachineClient client, IEvent evt)
        {
            Target.SetGeometryState("pageOn");
        }

        public sealed class Definition : InteractionDefinition<Sim, AlchemyStation, StudySpells>
        {
            protected override string GetInteractionName(Sim actor, AlchemyStation target, InteractionObjectPair pair)
            {
                return JournalText.Get("Arcana.Station.StudySpells");
            }

            protected override bool Test(Sim actor, AlchemyStation target, bool autonomous, ref GreyedOutTooltipCallback tooltip)
            {
                return !autonomous && SpellStudy.TrackFor(actor) != null;
            }
        }
    }
}
