﻿using System;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk
{
    public class MessagesObserver
    {
        public static Message LastMessage;

        public static void Update()
        {
            var msges = MyStrategy.Self.Messages;
            if (msges != null && msges.Length > 0)
                LastMessage = msges[msges.Length - 1];
        }


        public static ALaneType GetLane()
        {
            return ALaneType.Middle;
            if (LastMessage != null)
                return (ALaneType) LastMessage.Lane;

            var arr = new [] { LaneType.Top, LaneType.Bottom };
            return (ALaneType) arr[MyStrategy.Self.Id%arr.Length];
        }

        private static SkillType? _getNearestSkill(AWizard self, SkillType skill)
        {
            var skillGroup = Utility.GetSkillGroup(skill);
            if (self.SkillsLearnedArr[skillGroup] == 5)
                return null;

            return Utility.GetSkill(skillGroup, self.SkillsLearnedArr[skillGroup]);
        }

        public static SkillType GetSkill()
        {
            var self = MyStrategy.ASelf;


            var skillsOrder = new[]
            {
                SkillType.Fireball,
                SkillType.RangeBonusPassive1,
                SkillType.Haste,
                SkillType.AdvancedMagicMissile,

                SkillType.FrostBolt,
                SkillType.Shield
            };

            foreach (var skill in skillsOrder)
            {
                var skillToLearn = _getNearestSkill(self, skill);
                if (skillToLearn != null)
                    return (SkillType)skillToLearn;
            }
            throw new Exception("An impossible state");
        }
    }
}
