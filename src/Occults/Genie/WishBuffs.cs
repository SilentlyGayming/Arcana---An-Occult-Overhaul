using Sims3.UI.Hud;
using System;
using System.Collections.Generic;
using Sims3.Gameplay.Utilities;
using Sims3.Gameplay.ActorSystems;
using Sims3.Gameplay.Actors;
using Sims3.Gameplay.CAS;
using Sims3.Gameplay.Socializing;
using Sims3.SimIFace;
using Sims3.SimIFace.CAS;
using Sims3.SimIFace.CustomContent;

namespace SupernaturalExpanded.Journal
{
    internal static class WishBuffs
    {
        internal static readonly BuffNames GoneWrong = (BuffNames)ResourceUtils.HashString64("ArcanaWishGoneWrong");
        internal static readonly BuffNames TarnishedBeauty = (BuffNames)ResourceUtils.HashString64("ArcanaWishTarnishedBeauty");
        internal static readonly BuffNames BorrowedJoy = (BuffNames)ResourceUtils.HashString64("ArcanaWishBorrowedJoy");
        internal static readonly BuffNames CareerSetback = (BuffNames)ResourceUtils.HashString64("ArcanaWishCareerSetback");
        internal static readonly BuffNames OneSidedLove = (BuffNames)ResourceUtils.HashString64("ArcanaWishOneSidedLove");

        internal static void Register()
        {
            Add(GoneWrong, "Arcana.Genie.WishBuff.GoneWrong.Name", "Arcana.Genie.WishBuff.GoneWrong.Description", -25, 24f, false, false);
            Add(TarnishedBeauty, "Arcana.Genie.WishBuff.Beauty.Name", "Arcana.Genie.WishBuff.Beauty.Description", -20, 48f, true, false);
            Add(BorrowedJoy, "Arcana.Genie.WishBuff.Joy.Name", "Arcana.Genie.WishBuff.Joy.Description", 25, 24f, false, false);
            Add(CareerSetback, "Arcana.Genie.WishBuff.Career.Name", "Arcana.Genie.WishBuff.Career.Description", -10, 24f, false, false);
            Add(OneSidedLove, "Arcana.Genie.WishBuff.Love.Name", "Arcana.Genie.WishBuff.Love.Description", 10, 24f, false, true);
        }

        private static void Add(BuffNames guid, string name, string description, int mood, float hours, bool romance, bool targeted)
        {
            if (BuffManager.BuffDictionary.ContainsKey((ulong)guid)) return;
            Buff.BuffData data = Data(guid, name, description, mood, hours, romance);
            Buff buff = targeted ? new UnrequitedWishBuff(data) : guid == BorrowedJoy ? (Buff)new BorrowedWishJoyBuff(data)
                : guid == GoneWrong ? (Buff)new WishGoneWrongBuff(data) : new Buff(data);
            BuffManager.BuffDictionary.Add((ulong)guid, buff.CreateBuffInstance());
            BuffManager.sBuffEnumValues.AddNewEnumValue(name, guid);
        }

        internal static Buff.BuffData GoneWrongData(bool romance)
        { return Data(GoneWrong, "Arcana.Genie.WishBuff.GoneWrong.Name", "Arcana.Genie.WishBuff.GoneWrong.Description", -25, 24f, romance); }

        private static Buff.BuffData Data(BuffNames guid, string name, string description, int mood, float hours, bool romance)
        {
            Buff.BuffData data = new Buff.BuffData();
            data.mBuffGuid = guid; data.mBuffName = name; data.mDescription = description; data.mHelpText = "";
            data.mEffectValue = mood; data.mTimeoutSimMinutes = hours * 60f;
            data.mAxisEffected = mood < 0 ? MoodAxis.Uncomfortable : MoodAxis.Happy;
            data.mPolarityOverride = mood < 0 ? Polarity.Negative : Polarity.Positive;
            data.mAvailabilityFlags = CASAGSAvailabilityFlags.HumanAgeMask;
            data.mIncreasedEffectivenessList = new List<SocialManager.SocialEffectiveness>();
            data.mReducedEffectivenessList = new List<SocialManager.SocialEffectiveness>();
            if (romance) data.mReducedEffectivenessList.Add(SocialManager.SocialEffectiveness.Romance);
            data.mDisallowedOccults = new List<OccultTypes>(); data.mNeededTraitList = new List<TraitNames>();
            data.mFacialIdles = new string[0]; data.mVersion = 1.0; data.mProductVersion = ProductVersion.EP6;
            data.mBuffCategory = BuffCategory.Mental;
            BuffInstance icon;
            if (BuffManager.BuffDictionary.TryGetValue((ulong)(mood < 0 ? BuffNames.GeniePowerFailure : BuffNames.WishForLove), out icon))
            { data.mThumbKey = icon.ThumbKey; data.mThumbString = icon.ThumbString; }
            return data;
        }

        internal static void GiveWrong(Sim sim, float hours) { GiveWrong(sim, hours, -25); }
        internal static void GiveWrong(Sim sim, float hours, int mood)
        {
            if (sim == null || sim.BuffManager == null) return;
            BuffInstance existing = sim.BuffManager.GetElement(GoneWrong);
            if (existing == null) sim.BuffManager.AddElementIgnoreDelay(GoneWrong, mood, hours * 60f, Origin.FromGenie);
            else
            {
                existing.TimeoutCount = Math.Max(existing.TimeoutCount, hours * 60f);
                if (existing.EffectValue > mood) sim.BuffManager.ChangeMoodValue(GoneWrong, mood);
            }
            WishState.Read(sim.SimDescription, true);
        }

        internal static bool HasBeautyPenalty(Sim sim)
        {
            if (sim == null || sim.BuffManager == null) return false;
            WishGoneWrongInstance wrong = sim.BuffManager.GetElement(GoneWrong) as WishGoneWrongInstance;
            return sim.BuffManager.HasElement(TarnishedBeauty) || wrong != null && wrong.RomanceEnds > SimClock.CurrentTicks;
        }

        internal static void GiveBeautyFailure(Sim sim, float hours)
        {
            GiveWrong(sim, hours, -20);
            WishGoneWrongInstance wrong = sim.BuffManager.GetElement(GoneWrong) as WishGoneWrongInstance;
            if (wrong != null) wrong.MarkBeauty(sim, hours);
        }

        internal static void CaptureTravel(Sim sim, out float wrong, out int mood, out float beauty, out float love, out ulong target, out float joy)
        {
            BuffInstance buff = sim.BuffManager.GetElement(GoneWrong);
            wrong = buff == null ? 0f : Math.Max(0f, buff.TimeoutCount);
            mood = buff == null ? -25 : buff.EffectValue;
            WishGoneWrongInstance bad = buff as WishGoneWrongInstance;
            beauty = bad == null || bad.RomanceEnds <= SimClock.CurrentTicks ? 0f
                : Math.Min(wrong, SimClock.ConvertFromTicks(bad.RomanceEnds - SimClock.CurrentTicks, TimeUnit.Minutes));
            UnrequitedWishInstance smitten = sim.BuffManager.GetElement(OneSidedLove) as UnrequitedWishInstance;
            love = smitten == null ? 0f : Math.Max(0f, smitten.TimeoutCount);
            target = smitten == null ? 0UL : smitten.SavedTargetId;
            buff = sim.BuffManager.GetElement(BorrowedJoy);
            joy = buff == null ? 0f : Math.Max(0f, buff.TimeoutCount);
        }

        internal static void RestoreTravel(Sim sim, float wrong, int mood, float beauty, float love, ulong target, float joy)
        {
            if (wrong > 0f && !sim.BuffManager.HasElement(GoneWrong)) GiveWrong(sim, wrong / 60f, mood);
            WishGoneWrongInstance bad = sim.BuffManager.GetElement(GoneWrong) as WishGoneWrongInstance;
            if (bad != null && beauty > 0f) bad.MarkBeauty(sim, beauty / 60f);
            if (love > 0f && !sim.BuffManager.HasElement(OneSidedLove))
            {
                sim.BuffManager.AddElementIgnoreDelay(OneSidedLove, 10, love, Origin.FromGenie);
                UnrequitedWishInstance smitten = sim.BuffManager.GetElement(OneSidedLove) as UnrequitedWishInstance;
                if (smitten != null) smitten.SavedTargetId = target;
            }
            if (joy > 0f && !sim.BuffManager.HasElement(BorrowedJoy))
            {
                sim.BuffManager.AddElementIgnoreDelay(BorrowedJoy, 25, joy, Origin.FromGenie);
                WishRecord state = WishState.Read(sim.SimDescription, true);
                state.JoyEnds = WishState.After(joy / 60f); state.SadEnds = WishState.After(joy / 60f + 24f);
            }
        }

        internal static void Refresh(Sim sim)
        {
            BuffInstance old = sim.BuffManager.GetElement(TarnishedBeauty);
            if (old != null)
            {
                float hours = Math.Max(0f, old.TimeoutCount / 60f);
                sim.BuffManager.RemoveElement(TarnishedBeauty);
                if (hours > 0f) GiveBeautyFailure(sim, hours);
            }
            old = sim.BuffManager.GetElement(CareerSetback);
            if (old != null)
            {
                float hours = Math.Max(0f, old.TimeoutCount / 60f);
                sim.BuffManager.RemoveElement(CareerSetback);
                if (hours > 0f) GiveWrong(sim, hours, -10);
            }
            WishGoneWrongInstance wrong = sim.BuffManager.GetElement(GoneWrong) as WishGoneWrongInstance;
            if (wrong != null) wrong.Refresh(sim, SimClock.CurrentTicks, false);
            UnrequitedWishInstance smitten = sim.BuffManager.GetElement(OneSidedLove) as UnrequitedWishInstance;
            if (smitten != null) sim.BuffManager.ChangeMoodValue(OneSidedLove, smitten.EffectValueForSim(sim));
        }
    }

    public sealed class WishGoneWrongBuff : Buff
    {
        public WishGoneWrongBuff(BuffData data) : base(data) { }
        public override BuffInstance CreateBuffInstance()
        { return new WishGoneWrongInstance(this, BuffGuid, EffectValue, TimeoutSimMinutes); }
        public override void OnAddition(BuffManager manager, BuffInstance instance, bool travelReaddition)
        {
            base.OnAddition(manager, instance, travelReaddition);
            WishGoneWrongInstance wrong = instance as WishGoneWrongInstance;
            if (wrong != null) wrong.Configure(SimClock.CurrentTicks);
        }
    }

    [Persistable]
    public sealed class WishGoneWrongInstance : BuffInstance
    {
        internal long RomanceEnds;
        private bool romanceApplied;
        public WishGoneWrongInstance() { }
        public WishGoneWrongInstance(Buff buff, BuffNames guid, int mood, float timeout) : base(buff, guid, mood, timeout) { }
        public override BuffInstance Clone() { return (WishGoneWrongInstance)MemberwiseClone(); }
        internal void MarkBeauty(Sim sim, float hours)
        {
            RomanceEnds = Math.Max(RomanceEnds, WishState.After(hours));
            Refresh(sim, SimClock.CurrentTicks, true);
        }
        internal void Configure(long now)
        {
            mBuff = new WishGoneWrongBuff(WishBuffs.GoneWrongData(RomanceEnds > now));
            romanceApplied = RomanceEnds > now;
        }
        internal void Refresh(Sim sim, long now, bool force)
        {
            bool active = RomanceEnds > now;
            if (!force && active == romanceApplied) return;
            bool previouslyApplied = romanceApplied;
            Configure(now);
            if (sim == null || sim.SocialComponent == null) return;
            if (active) sim.SocialComponent.AddReducedEffectiveness(SocialManager.SocialEffectiveness.Romance);
            else if (previouslyApplied) sim.SocialComponent.RemoveReducedEffectiveness(SocialManager.SocialEffectiveness.Romance);
        }
        public override bool OnLoadFixup(Sim sim)
        {
            bool valid = base.OnLoadFixup(sim);
            if (valid) Refresh(sim, SimClock.CurrentTicks, true);
            return valid;
        }
        public override bool ExportContent(ResKeyTable resources, ObjectIdTable objects, IPropertyStreamWriter writer)
        { return base.ExportContent(resources, objects, writer) && writer.WriteInt64(0xAC520102, RomanceEnds); }
        public override void ImportContentSpecial(ResKeyTable resources, ObjectIdTable objects, IPropertyStreamReader reader)
        {
            base.ImportContentSpecial(resources, objects, reader);
            reader.ReadInt64(0xAC520102, out RomanceEnds, 0);
            romanceApplied = false;
        }
    }

    public sealed class BorrowedWishJoyBuff : Buff
    {
        public BorrowedWishJoyBuff(BuffData data) : base(data) { }
        public override void OnTimeout(BuffManager manager, BuffInstance instance, OnTimeoutReasons reason)
        {
            base.OnTimeout(manager, instance, reason);
            WishState.JoyExpired(manager.Actor);
        }
    }

    public sealed class UnrequitedWishBuff : Buff
    {
        public UnrequitedWishBuff(BuffData data) : base(data) { }
        public override BuffInstance CreateBuffInstance()
        { return new UnrequitedWishInstance(this, BuffGuid, EffectValue, TimeoutSimMinutes); }
        public override void OnAddition(BuffManager manager, BuffInstance instance, bool travelReaddition)
        {
            base.OnAddition(manager, instance, travelReaddition);
            WishState.Read(manager.Actor.SimDescription, true);
        }
    }

    [Persistable]
    public sealed class UnrequitedWishInstance : BuffInstance
    {
        private SimDescription beloved;
        private ulong belovedId;
        internal ulong SavedTargetId { get { return belovedId; } set { belovedId = value; beloved = null; } }
        public UnrequitedWishInstance() { }
        public UnrequitedWishInstance(Buff buff, BuffNames guid, int mood, float timeout) : base(buff, guid, mood, timeout) { }
        public override BuffInstance Clone() { return (UnrequitedWishInstance)MemberwiseClone(); }
        public override SimDescription TargetSim { get { return beloved ?? SimDescription.Find(belovedId); } }
        public override void SetTargetSim(SimDescription target) { beloved = target; belovedId = target == null ? 0UL : target.SimDescriptionId; }
        public override bool ExportContent(ResKeyTable resources, ObjectIdTable objects, IPropertyStreamWriter writer)
        { return base.ExportContent(resources, objects, writer) && writer.WriteUint64(0xAC520101, belovedId); }
        public override void ImportContentSpecial(ResKeyTable resources, ObjectIdTable objects, IPropertyStreamReader reader)
        {
            base.ImportContentSpecial(resources, objects, reader);
            reader.ReadUint64(0xAC520101, out belovedId, 0); beloved = null;
        }
        public override int EffectValueForSim(Sim sim)
        {
            if (sim == null) return 0;
            SimDescription subject = TargetSim;
            Sim other = subject == null ? null : subject.CreatedSim;
            return other != null && other.LotCurrent == sim.LotCurrent && (other.Position - sim.Position).Length() < 10f ? 10 : 0;
        }
    }
}
