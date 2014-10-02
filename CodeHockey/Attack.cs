﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms.PropertyGridInternal;
using System.Windows.Forms.VisualStyles;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;
using Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk
{
    public partial class MyStrategy : IStrategy
    {
        public ArrayList WayPoints;

        public int GetWayPointIndex(Point wayPoint)
        {
            for (var i = 0; i < WayPoints.Count; i++)
                if (wayPoint.Same(WayPoints[i] as Point))
                    return i;
            throw new Exception("Unknown waypoint");
        }

        void FillWayPoints()
        {
            WayPoints = new ArrayList
            {
                new Point(286, 375),
                new Point(443, 325),
                new Point(620, 300),
                new Point(785, 325),
                new Point(917, 375),

                new Point(442, 226),
                new Point(622, 216),
                new Point(787, 235),

                new Point(284, 266),
                new Point(919, 264),
                new Point(617, 363),
            };
            if (MyRight())
            {
                WayPoints.Add(new Point(883, 463));
                WayPoints.Add(new Point(691, 454));
            }
            else
            {
                WayPoints.Add(new Point(545, 454));
                WayPoints.Add(new Point(328, 457));
            }

            if (MyRight())
                WayPoints.Reverse();

            var len = WayPoints.Count;
            for (var i = 0; i < len; i++)
            {
                var a = WayPoints[i] as Point;
                WayPoints.Add(new Point(a.X, Game.RinkBottom - (a.Y - Game.RinkTop)));
            }
        }

        public void MoveTo(AHock hock, Point p)
        {
            var turn = hock.GetAngleTo(p);
            var speedUp = GetSpeedTo(turn);
            hock.Move(speedUp, TurnNorm(turn, hock.Base.Agility));
        }

        public Point FindWayPoint(Hockeyist self)
        {
            double OkDist = 5 * HoRadius;

            var bestTime = Inf;
            Point sel = null;
            foreach (Point p in WayPoints)
            {
                var I = new AHock(self);
                if (p.GetDistanceTo2(I) <= OkDist * OkDist || MyRight() && I.X < p.X || MyLeft() && I.X > p.X)
                    continue;

                var cands = World.Hockeyists
                    .Where(x => !x.IsTeammate && IsInGame(x))
                    .Select(x => new AHock(x)).ToArray();

                var time = 0;
                var ok = true;
                while (p.GetDistanceTo2(I) > OkDist * OkDist && ok)
                {
                    MoveTo(I, p);
                    foreach (var c in cands)
                    {
                        MoveTo(c, I);
                        if (CanStrike(c, I.PuckPos()) // достанет шайбу
                            || CanStrike(c, I) // достанет меня
                            || I.GetDistanceTo2(c) <= 2 * HoRadius * 2 * HoRadius // столкнется со мной
                            )
                        {
                            ok = false;
                            break;
                        }
                    }
                    time++;
                }
                if (ok)
                {
                    if (time < bestTime)
                    {
                        bestTime = time;
                        sel = new Point(p);
                    }
                }
            }
            return sel;
        }

        double StrikeProbability(AHock striker, double strikePower, Point goalie, int leftTime, ActionType actionType, double passAngle)
        {
            if (striker.CoolDown != 0)
                return 0.0;

            var deviation = (actionType == ActionType.Strike ? Game.StrikeAngleDeviation : Game.PassAngleDeviation)*100/striker.Base.Dexterity;
            double range = deviation * 2,
                dx = deviation / 20,
                result = 0;

            for (var dir = -1; dir <= 1; dir += 2)
            {
                double last = Inf;
                for (var _x = 0.0; _x <= range; _x += dx)
                {
                    if (Eq(_x, 0) && dir == -1)
                        continue;
                    var x = _x*dir;
                    var y = 0.0;

                    if (actionType == ActionType.Strike)
                        striker.Angle += x;
                    if (Strike(striker, strikePower, goalie, actionType, passAngle + x))
                    {
                        y = dx*Gauss(x, 0, deviation);
                        result += y;
                    }
                    if (actionType == ActionType.Strike)
                        striker.Angle -= x;

                    if (y + last < Eps)
                        break;
                    last = y;
                }
            }

            // Проверка что шайбу перехватят:
            if (leftTime != -1)
            {
                var pk = actionType == ActionType.Strike
                    ? GetStrikePuck(striker, strikePower, goalie)
                    : GetPassPuck(striker, 1, passAngle);
                var opps = World.Hockeyists
                    .Where(x => !x.IsTeammate && IsInGame(x))
                    .Select(x => new AHock(x)).ToArray();

                pk.Clone().Move(300, true);
                var time = APuck.PuckLastTicks;
                for (var t = -leftTime; t < time; t++)
                {
                    foreach (var opp in opps)
                    {
                        var hisTurn = TurnNorm(opp.GetAngleTo(pk), opp.Base.Agility);
                        opp.Move(0.0, hisTurn);
                        if (CanStrike(opp, pk))
                        {
                            var pTake = (75.0 + Math.Max(opp.Base.Dexterity, opp.Base.Agility) -
                                         pk.Speed.Length/20*100)/100;
                            return result*(1 - pTake);
                        }
                    }
                    if (t >= 0)
                        pk.Move(1);
                }
            }
            return result;
        }

        APuck GetStrikePuck(AHock striker, double strikePower, Point goalie)
        {
            var strikerDirection = new Point(striker.Angle);
            var speedAngleStriker = striker.Speed.GetAngle();
            var puckSpeed = 20.0 * strikePower + striker.Speed.Length * Math.Cos(striker.Angle - speedAngleStriker);
            var puckSpeedDirection = strikerDirection * puckSpeed;
            return new APuck(striker.PuckPos(), puckSpeedDirection, goalie);
        }

        bool StrikePrimitiveValidate(AHock striker)
        {
            var puckPos = striker.PuckPos();
            if (Math.Abs(puckPos.X - Opp.NetFront) > RinkWidth / 2)
                return false;

            if (Math.Abs(puckPos.X - Opp.NetFront) < 3.5 * HoRadius)
                return false;

            if (MyRight() && Math.Cos(striker.Angle) > 0)
                return false;
            if (MyLeft() && Math.Cos(striker.Angle) < 0)
                return false;

            return true;
        }

        bool Strike(AHock striker, double strikePower, Point goalie, ActionType actionType, double passAngle)
        {
            if (!StrikePrimitiveValidate(striker))
                return false;

            var pk = actionType == ActionType.Strike
                ? GetStrikePuck(striker, strikePower, goalie)
                : GetPassPuck(striker, 1, passAngle);
            return pk.Move(300, true) == 1;
        }

        bool Chase(IEnumerable<AHock> opps, AHock I)
        {
            foreach (var opp in opps)
            {
                MoveTo(opp, I);
                if (CanStrike(opp, I) || CanStrike(opp, I.PuckPos()))
                    return false;
            }
            return true;
        }

        double ProbabStrikeAfter(int swingTime, Hockeyist self, IEnumerable<MoveAction> actions, ActionType actionType)
        {
            var power = GetPower(self, swingTime);
            var I = new AHock(self);
            var totalTime = 0;
            var opps = World.Hockeyists
                .Where(x => !x.IsTeammate && IsInGame(x))
                .Select(x => new AHock(x))
                .ToArray();

            foreach (var action in actions)
            {
                for (var i = 0; i < action.Ticks; i++)
                {
                    I.Move(action.SpeedUp, action.Turn);
                    if (!Chase(opps, I))
                        return 0.0;
                }
                totalTime += action.Ticks;
            }
            var pk = GetPuckPos(I, I.Angle);
            var goalie = Get(OppGoalie);
            GoalieMove(goalie, totalTime, pk);
            var passAngle = PassAngleNorm(I.GetAngleTo(GetStrikePoint()));
            return StrikeProbability(I, power, goalie, totalTime, actionType, passAngle);
        }

        public static double PassAngleNorm(double angle)
        {
            if (angle > Game.PassSector/2)
                return Game.PassSector/2;
            if (angle < -Game.PassSector/2)
                return -Game.PassSector/2;
            return angle;
        }

        public Point GetStrikePoint()
        {
            const double delta = 1;
            const double shift = 10;
            double x = Opp.NetFront,
                bestDist = 0,
                bestY = 0;
            var OppGoalie = Get(MyStrategy.OppGoalie) ?? Point.Zero;
            for (var y = Opp.NetTop + shift; y <= Opp.NetBottom - shift; y += delta)
            {
                if (OppGoalie.GetDistanceTo2(x, y) > bestDist * bestDist)
                {
                    bestDist = OppGoalie.GetDistanceTo(x, y);
                    bestY = y;
                }
            }
            return new Point(x, bestY);
        }

        public bool TryStrikeWithoutSwing(AHock _hock, APuck _pk)
        {
            if (!StrikePrimitiveValidate(_hock))
                return false;

            const double dTurn = 0.01;
            var moveDir = MyRight() && _hock.Y > RinkCenter.Y || MyLeft() && _hock.Y < RinkCenter.Y ? 1 : -1;

            var bestTurn = 0.0;
            var bestSpUp = 0.0;
            var bestProbab = 0.0;
            var bestWait = Inf;

            for (var moveTurn = 0.0; moveTurn <= 3*dTurn; moveTurn += dTurn)
            {
                var turn = moveDir*moveTurn;

                for (var spUp = 0.0; spUp <= 1.0; spUp += 1/3.0)
                {
                    var hock = _hock.Clone();
                    var pk = _pk.Clone();
                    var ticksWait = 0;
                    for (var startDist2 = hock.GetDistanceTo2(pk); !CanStrike(hock, pk) && ticksWait < 150; ticksWait++)
                    {
                        hock.Move(spUp, turn);
                        pk.Move(1);
                        var dist2 = hock.GetDistanceTo2(pk);
                        if (dist2 > startDist2)
                            break;
                        startDist2 = dist2;
                    }
                    if (CanStrike(hock, pk) &&
                        Strike(hock, GetPower(hock.Base, 0), Get(OppGoalie), ActionType.Strike, 0))
                    {
                        var p = StrikeProbability(hock,
                            GetPower(hock.Base, 0), Get(OppGoalie), -1, ActionType.Strike, 0);
                        if (p > bestProbab)
                        {
                            bestProbab = p;
                            bestTurn = turn;
                            bestSpUp = spUp;
                            bestWait = ticksWait;
                        }
                    }
                }
            }
            if (bestWait == Inf)
                return false;
            move.Turn = bestTurn;
            move.SpeedUp = bestSpUp;
            if (bestWait == 0)
                move.Action = ActionType.Strike;
            return true;
        }
    }
}