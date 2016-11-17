﻿using System;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;
using Microsoft.Win32.SafeHandles;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk
{
    public abstract class AMinion : ACombatUnit
    {
        public bool IsAggressiveNeutral;

        protected AMinion(Minion unit) : base(unit)
        {
        }

        protected AMinion(AMinion unit) : base(unit)
        {
            IsAggressiveNeutral = unit.IsAggressiveNeutral;
        }

        public override void SkipTick()
        {
            if (RemainingActionCooldownTicks > 0)
                RemainingActionCooldownTicks--;
        }

        public override void EthalonMove(ACircularUnit target)
        {
            SkipTick();
        }

        public override bool IsOpponent => IsAggressiveNeutral || !IsTeammate && (Faction == Faction.Academy || Faction == Faction.Renegades);
        public bool IsNeutral => Faction == Faction.Neutral;
    }

    public class AOrc : AMinion
    {
        public AOrc(Minion minion) : base(minion)
        {
            
        }

        public AOrc(AMinion minion) : base(minion)
        {

        }

        public override bool EthalonCanHit(ACircularUnit target)
        {
            var angleTo = GetAngleTo(target);
            if (Math.Abs(angleTo) > MyStrategy.Game.OrcWoodcutterAttackSector/2)
                return false;
            return GetDistanceTo2(target) <= Geom.Sqr(MyStrategy.Game.OrcWoodcutterAttackRange + target.Radius);
        }
    }

    public class AFetish : AMinion
    {
        public AFetish(Minion minion) : base(minion)
        {

        }

        public AFetish(AMinion minion) : base(minion)
        {

        }

        public override bool EthalonCanHit(ACircularUnit target)
        {
            var angleTo = GetAngleTo(target);
            if (Math.Abs(angleTo) > MyStrategy.Game.FetishBlowdartAttackSector / 2)
                return false;
            return GetDistanceTo2(target) <= Geom.Sqr(MyStrategy.Game.FetishBlowdartAttackRange + target.Radius + MyStrategy.Game.DartRadius);
        }
    }
}
