﻿using System.Collections.Generic;
using System.Linq;
using Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWizards2016.DevKit.CSharpCgdk
{
    class ProjectilesObserver
    {
        private static Dictionary<long, AProjectile> _projectiles = new Dictionary<long, AProjectile>();
        private static Dictionary<long, AWizard> _wizardsLastSeen = new Dictionary<long, AWizard>();

        public static void Update()
        {
            var projectiles = MyStrategy.World.Projectiles
                .Select(x => new AProjectile(x))
                .Where(x => !x.IsFriendly || x.Type == ProjectileType.Dart) // свои Dart тоже могут нанести урон
                .ToArray();

            var newDict = new Dictionary<long, AProjectile>();
            foreach (var w in MyStrategy.OpponentWizards)
                _wizardsLastSeen[w.Id] = w;

            foreach (var proj in projectiles)
            {
                if (_projectiles.ContainsKey(proj.Id))
                {
                    // был и сейчас есть
                    var p = _projectiles[proj.Id];
                    newDict[p.Id] = p;
                    p.Move();
                    // check: p.X == proj.X, p.Y = proj.Y
                }
                else
                {
                    // только появился
                    var owner = MyStrategy.OpponentCombats.FirstOrDefault(x => x.Id == proj.OwnerUnitId);
                    if (owner == null && _wizardsLastSeen.ContainsKey(proj.OwnerUnitId))
                    {
                        // его снаряд видно, но самого не видно
                        owner = _wizardsLastSeen[proj.OwnerUnitId];
                    }
                    var castRange = owner?.CastRange ??
                                    (proj.Type == ProjectileType.Dart
                                        ? MyStrategy.Game.FetishBlowdartAttackRange
                                        : MyStrategy.Game.IsSkillsEnabled ? 600 : 500);
                    
                    newDict[proj.Id] = proj;
                    proj.RemainingDistance = castRange - proj.Speed;

                    if (owner != null)
                        proj.SetupDamage(owner);
                }
            }

            var myFireball = MyStrategy.World.Projectiles.FirstOrDefault(
                x => x.Type == ProjectileType.Fireball && x.OwnerUnitId == MyStrategy.Self.Id);
            if (myFireball != null)
            {
                newDict[myFireball.Id] = new AProjectile(myFireball)
                {
                    RemainingDistance = MyFireballExplosionPoint.GetDistanceTo(myFireball)
                };
            }


            // которые были и пропали не попадут в newDict

            _projectiles = newDict;

#if DEBUG
            if (MyStrategy.World.TickIndex == MyStrategy._lastProjectileTick + 1)
            {
                foreach (var pr in MyStrategy.World.Projectiles)
                {
                    if (pr.OwnerUnitId == MyStrategy.Self.Id)
                    {
                        Visualizer.Visualizer.Projectiles[pr.Id] = MyStrategy._lastProjectilePoints;
                    }
                }
            }
#endif
        }

        public static Point MyFireballExplosionPoint = Point.Zero;

        public static AProjectile[] Projectiles => _projectiles.Values.ToArray();
    }
}
