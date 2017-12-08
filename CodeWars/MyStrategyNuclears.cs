﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
    public partial class MyStrategy
    {
        double asdf(AVehicle veh, Sandbox env, double lowerBound, List<AVehicle> targets, out ANuclear nuclearResult)
        {
            var vr = veh.ActualVisionRange * 0.9;
            var cen = Utility.Average(targets);
            cen = veh + (cen - veh).Normalized() * Math.Min(vr, veh.GetDistanceTo(cen));
            var nuclear = new ANuclear(cen.X, cen.Y, true, veh.Id, G.TacticalNuclearStrikeDelay);
            nuclearResult = nuclear;

            var totalOpponentDamage = targets.Sum(x => x.GetNuclearDamage(nuclear));
            if (totalOpponentDamage <= lowerBound)
                return totalOpponentDamage;

            var totalDamage = totalOpponentDamage -
                  env.GetMyNeighbours(nuclear.X, nuclear.Y, nuclear.Radius)
                      .Sum(x => x.GetNuclearDamage(nuclear));

            return totalDamage;
        }

        AMove NuclearStrategy()
        {
            AMove result = null;
            if (Me.RemainingNuclearStrikeCooldownTicks == 0)
            {
                Logger.CumulativeOperationStart("NuclearStrategy");
                result = _nuclearStrategy();
                Logger.CumulativeOperationEnd("NuclearStrategy");
            }
            return result;
        }

        Tuple<double, AMove> fnd(Sandbox env, double selTotalDamage, bool checkOnly)
        {
            AMove selMove = null;

            for (var s = 0; s < GroupsManager.MyGroups.Count + MyUngroupedClusters.Count; s++)
            {
                var vehicles = s < GroupsManager.MyGroups.Count
                    ? env.GetVehicles(true, GroupsManager.MyGroups[s])
                    : MyUngroupedClusters[s - GroupsManager.MyGroups.Count];
                var myAvg = Utility.Average(vehicles);

                var vrg = G.VisionRange[(int) VehicleType.Fighter] + G.MaxTacticalNuclearStrikeDamage;

                var oppGroups = OppClusters
                    .Where(cl => cl.Avg.GetDistanceTo(myAvg) < vrg)
                    .OrderBy(cl => cl.Avg.GetDistanceTo(myAvg))
                    .Take(3)
                    .ToArray();

                foreach (var veh in vehicles)
                {
                    var vr = veh.ActualVisionRange * 0.9;

                    foreach (
                        var oppGroup in
                            new[] {env.GetOpponentNeighbours(veh.X, veh.Y, vr + G.TacticalNuclearStrikeRadius)}.Concat(oppGroups))
                    {
                        ANuclear nuclear;
                        var totalDamage = asdf(veh, env, selTotalDamage, oppGroup, out nuclear);

                        if (totalDamage <= selTotalDamage)
                            continue;

                        var vehNextMove = new AVehicle(veh);
                        vehNextMove.Move();
                        if (vehNextMove.GetDistanceTo2(nuclear) + Const.Eps >= Geom.Sqr(vehNextMove.ActualVisionRange))
                            continue;

                        const int n = 10;
                        if (vehicles.Count > n)
                        {
                            var myDist2 = veh.GetDistanceTo2(nuclear);
                            var myNearestCount = vehicles.Count(x => x.GetDistanceTo2(nuclear) <= myDist2);
                            if (myNearestCount < n)
                                continue;
                        }

                        selTotalDamage = totalDamage;
                        selMove = new AMove
                        {
                            Action = ActionType.TacticalNuclearStrike,
                            VehicleId = veh.Id,
                            Point = nuclear,
                        };

                        if (checkOnly)
                            return new Tuple<double, AMove>(selTotalDamage, selMove);
                    }
                }
            }

            if (selMove == null)
            {
                return null;
            }

            return new Tuple<double, AMove>(selTotalDamage, selMove);
        }

        AMove _nuclearStrategy()
        {
            var damageBound2 = 8000.0 * Environment.Vehicles.Length / 1000;
            var damageBound1 = 3000.0 * Environment.Vehicles.Length / 1000;

            var cur = fnd(Environment, damageBound1, false);
            if (cur == null)
            {
                _prevNuclearTotalDamage = 0;
                return null;
            }

            if (cur.Item1 >= damageBound2)
            {
                _prevNuclearTotalDamage = 0;
                return cur.Item2;
            }

            // нужно проверить, что в следующий тик не будет лучше

            // предыдущее предсказание не оправдалось:
            if (cur.Item1 < _prevNuclearTotalDamage)
            {
                _prevNuclearTotalDamage = 0;
                // возвращает то что есть
                return cur.Item2;
            }

            var env = Environment.Clone();
            env.DoTick(fight: false);

            var next = fnd(env, cur.Item1, true);
            if (next == null)
            {
                _prevNuclearTotalDamage = 0;
                return cur.Item2;
            }

            // должно буть лучше
            _prevNuclearTotalDamage = cur.Item1;
            return null;
        }

        private double _prevNuclearTotalDamage;
    }
}
