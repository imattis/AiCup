using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms.VisualStyles;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;
using Point = Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk.Point;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk 
{
    public partial class MyStrategy : IStrategy
    {
        public static double FrictionPuckCoeff = 0.999;
        public static double FrictionHockCoeff = 0.98;

        public Puck puck;
        public Move move;
        public static Player opp, my;
        private Hockeyist oppGoalie;
        private Hockeyist myGoalie;
        public static World world;
        public static Game game;
        
        public static double HoRadius;
        public static double RinkWidth, RinkHeight;
        public static Point RinkCenter;
        public static double PuckRadius;

        public Point GetStrikePoint()
        {
            double delta = 1;
            double shift = 15;
            double x = opp.NetFront;
            double bestDist = 0;
            double bestY = 0;
            double minY = Math.Min(opp.NetBottom, opp.NetTop);
            double maxY = Math.Max(opp.NetBottom, opp.NetTop);
            for (double y = minY + shift; y <= maxY - shift; y += delta)
            {
                if (oppGoalie.GetDistanceTo(x, y) > bestDist)
                {
                    bestDist = oppGoalie.GetDistanceTo(x, y);
                    bestY = y;
                }
            }
            return new Point(x, bestY);
        }

        Point GetStrikeFrom(Point myPositionPoint, Point mySpeed, double myAngle)
        {
            var x1 = game.RinkLeft + RinkWidth * 0.4;
            var x2 = game.RinkRight - RinkWidth * 0.4;
            var y1 = game.RinkTop + RinkHeight * 0.18;
            var y2 = game.RinkBottom - RinkHeight * 0.18;

            var a = new Point(MyRight() ? x1 : x2, y1);
            var b = new Point(MyRight() ? x1 : x2, y2);
            if (Math.Abs(myAngle) < Deg(20) || Math.Abs(myAngle) > Deg(160))
                return a.GetDistanceTo(myPositionPoint) < b.GetDistanceTo(myPositionPoint) ? a : b;
            return GetTicksTo(myPositionPoint, mySpeed, myAngle, a) < GetTicksTo(myPositionPoint, mySpeed, myAngle, b) ? a : b;
        }

        public double GetTicksTo(Point myPosition, Point mySpeed, double myAngle, Point to)
        {
            var totalSpeed = mySpeed.Length;
            var an = Point.GetAngleBetween(to, myPosition, new Point(myPosition.X + mySpeed.X, myPosition.Y + mySpeed.Y));
            var speed = totalSpeed * Math.Cos(an);
            return Math.Sqrt(speed*speed + 2*myPosition.GetDistanceTo(to.X, to.Y)) - speed;
        }

        public Point GoToPuck(Point myPosition, Point mySpeed, double myAngle, out int ticks)
        {
            double best = Inf;
            var result = new Point(myPosition);
            for (ticks = 0; ticks < 400; ticks++)
            {
                var puckPosition = PuckMove(ticks, new Point(puck), new Point(puck.SpeedX, puck.SpeedY));
                var needTicks = GetTicksTo(myPosition, mySpeed, myAngle, puckPosition);

                if (needTicks <= ticks && ticks - needTicks < best)
                {
                    best = ticks - needTicks;
                    result = puckPosition;
                }
            }
            return result;   
        }

        public Point GoToPuck(Point myPosition, Point mySpeed, double myAngle)
        {
            int ticks;
            return GoToPuck(myPosition, mySpeed, myAngle, out ticks);
        }

        Point GetDefendPos2(Point myPosition)
        {
            var y = myGoalie.Y > RinkCenter.Y ? my.NetTop + 1.2 * HoRadius : my.NetBottom - 1.2 * HoRadius;
            var a = new Point(game.RinkLeft + RinkWidth * 0.07, y);
            var b = new Point(game.RinkRight - RinkWidth * 0.07, y);
            return MyLeft() ? a : b;
        }

        public void StayOn(Hockeyist self, Point to, double needAngle)
        {
            if (to.GetDistanceTo(self) < 1.5 * HoRadius)
            {
                move.SpeedUp = 0;
                move.Turn = needAngle;
                return;
            }

            var v0 = GetSpeed(self).Length;
            var S = to.GetDistanceTo(self);
            var s1Res = 0.0;
            var tRes = Inf + 0.0;
            for (double s1 = 0; s1 <= S; s1 += 2)
            {
                var s2 = S - s1;
                var t1 = Math.Sqrt(v0 * v0 + 2 * s1) - v0;
                var vm = v0 + t1;
                var a = vm * vm / 2 / s2;
                var t2 = Math.Sqrt(2 * s2 / a);
                var t = t1 + t2;
                if (t < tRes)
                {
                    tRes = t;
                    s1Res = s1;
                }
            }

            double angle = self.GetAngleTo(to.X, to.Y); // �������� �� ������ ��������

            if (Math.Abs(angle) > Deg(90))
                move.Turn = angle < 0 ? Deg(180) + angle : angle - Deg(180); // ??
            else
                move.Turn = angle;

            if (s1Res > Eps)
            {
                move.SpeedUp = Math.Abs(angle) > Deg(90) ? -1 : 1;
            }
            else
            {
                var curSpeed = GetSpeed(self).Length;
                var restDist = to.GetDistanceTo(self);
                var a = curSpeed * curSpeed / 2 / restDist;
                move.SpeedUp = a;
            }
        }

        public void StayOn_(Hockeyist self, Point to, double needAngle)
        {
            if (to.GetDistanceTo(self) < 2 * HoRadius)
            {
                var dx = self.Angle * Math.Cos(self.Angle) / 100;
                var dy = self.Angle * Math.Sin(self.Angle) / 100;
                var speed = GetSpeed(self);
                var an = Math.Abs(AngleNormalize(self.GetAngleTo(speed.X, speed.Y)));
                if (to.GetDistanceTo(self.X + dx, self.Y + dy) < to.GetDistanceTo(self.X - dx, self.Y - dy))
                {
                    move.SpeedUp = an > Deg(90) ? 1 : 0.2;
                    move.Turn = needAngle;
                }
                else
                {
                    move.SpeedUp = an < Deg(90) ? -1 : -0.2;
                    //move.Turn = needAngle < 0 ? Deg(180) + needAngle : needAngle - Deg(180);
                    move.Turn = needAngle;
                }
                return;
            }

            var v0 = GetSpeed(self).Length;
            var S = to.GetDistanceTo(self);
            var s1Res = 0.0;
            var tRes = Inf + 0.0;

            double aS = 0.116;
            double angle = AngleNormalize(self.GetAngleTo(to.X, to.Y)); // �������� �� ������ ��������?

            if (Math.Abs(angle) > Deg(90))
            {
                move.Turn = angle < 0 ? Deg(180) + angle : angle - Deg(180); // ??
                aS = 0.069;
            }
            else
                move.Turn = angle;

            for (double s1 = 0; s1 <= S; s1 += 0.5)
            {
                var s2 = S - s1;
                var t1 = (Math.Sqrt(v0 * v0 + 2 * aS * s1) - v0) / aS;
                var vm = v0 + aS * t1;
                var a = vm*vm/2/s2;
                var t2 = Math.Sqrt(2*s2/a);
                var t = t1 + t2;
                if (t < tRes && IsBetween(-1.02, a / aS, 1.02))
                {
                    tRes = t;
                    s1Res = s1;
                }
            }
            
            if (s1Res > Eps)
            {
                move.SpeedUp = Math.Abs(angle) > Deg(90) ? -1 : 1;
            }
            else
            {
                var a = v0 * v0/ 2 / S;
                if (Math.Abs(angle) < Deg(90))
                    a = -a;
                move.SpeedUp = a / aS;
            }
        }

        double ProbabStrikeAfter(int wait, int swingTime, Hockeyist self, IEnumerable<Tuple<int, double, double>> move)
        {
            // TODO: use game.StrikePowerGrowthFactor
            var power = Math.Min(game.MaxEffectiveSwingTicks, swingTime) * 0.25 / game.MaxEffectiveSwingTicks + 0.75;
            var I = new AHo(Get(self), GetSpeed(self), self.Angle, self.AngularSpeed, self);
            foreach(var action in move)
            {
                I.Move(action.Second, action.Third, action.First);
            }
            return StrikeProbability(new Point(I.Angle) * self.GetDistanceTo(puck) + I, I.Speed, power, I.Angle);
        }

        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            ShowWindow();
            this.move = move;
            MyStrategy.world = world;
            MyStrategy.game = game;
            this.puck = world.Puck;
            MyStrategy.opp = world.GetOpponentPlayer();
            MyStrategy.my = world.GetMyPlayer();
            MyStrategy.RinkWidth = game.RinkRight - game.RinkLeft;
            MyStrategy.RinkHeight = game.RinkBottom - game.RinkTop;
            this.oppGoalie = world.Hockeyists.FirstOrDefault(x => !x.IsTeammate && x.Type == HockeyistType.Goalie);
            this.myGoalie = world.Hockeyists.FirstOrDefault(x => x.IsTeammate && x.Type == HockeyistType.Goalie);
            MyStrategy.HoRadius = self.Radius;
            MyStrategy.RinkCenter = new Point(game.RinkLeft + RinkWidth/2, game.RinkTop + RinkHeight/2);
            MyStrategy.PuckRadius = puck.Radius;
            var friend =
                world.Hockeyists.FirstOrDefault(x => x.IsTeammate && x.Id != self.Id && x.Type != HockeyistType.Goalie);

            if (null == oppGoalie)
                return;
            move.SpeedUp = Inf;

            var net = GetStrikePoint();
            var angleToNet = self.GetAngleTo(net.X, net.Y);
            var power = Math.Min(game.MaxEffectiveSwingTicks, self.SwingTicks)*0.25/game.MaxEffectiveSwingTicks + 0.75;

            if (self.State == HockeyistState.Swinging && self.Id != puck.OwnerHockeyistId)
            {
                move.Action = ActionType.CancelStrike;
            }
            else if (puck.OwnerHockeyistId == self.Id)
            {
                drawInfo.Enqueue(
                    StrikeProbability(Get(puck), GetSpeed(self),
                        Math.Min(game.MaxEffectiveSwingTicks, self.SwingTicks)*0.25/game.MaxEffectiveSwingTicks + 0.75,
                        self.Angle) + "");

                move.Turn = angleToNet;
                int wait = Inf;
                double selTurn = 0, selSpeedUp = 0;
                bool willSwing = false;
                double maxProb = 0.6;

                if (self.State != HockeyistState.Swinging)
                {
                    // ���� �� ����������
                    for (int ticks = 0; ticks < 40; ticks++)
                    {
                        double p;
                        // ���� ���� ������������ (�� � �����!!!), �� ����� ��������� ������� game.SwingActionCooldownTicks
                        var da = 0.01;
                        for (int dir = -1; dir <= 1; dir += 2)
                        {
                            for (var _turn = 0.0; _turn <= 2*da; _turn += da)
                            {
                                if (_turn == 0 && dir == 1)
                                    continue;
                                var turn = dir*_turn;

                                var end = ticks + game.SwingActionCooldownTicks;
                                var start = Math.Max(0, end - game.MaxEffectiveSwingTicks);
                                // ����� �������� ������������
                                p = ProbabStrikeAfter(start, end - start, self, new[]
                                {
                                    new Tuple<int, double, double>(start, 1, turn),
                                    new Tuple<int, double, double>(end - start, 0, 0)
                                });
                                if (p > maxProb)
                                {
                                    wait = start;
                                    willSwing = true;
                                    maxProb = p;
                                    selTurn = turn;
                                    selSpeedUp = 1;
                                }

                                // ���� �� ����
                                p = ProbabStrikeAfter(ticks, 0, self,
                                    new[] {new Tuple<int, double, double>(ticks, 0, turn)});
                                if (p > maxProb)
                                {
                                    wait = ticks;
                                    willSwing = false;
                                    maxProb = p;
                                    selTurn = turn;
                                    selSpeedUp = 0;
                                }
                            }
                        }
                    }
                }
                else
                {
                    // ���� ��� ����������
                    for (int ticks = Math.Max(0, game.SwingActionCooldownTicks - self.SwingTicks); ticks < 60; ticks++)
                    {
                        var p = ProbabStrikeAfter(ticks, ticks + self.SwingTicks, self,
                            new[] {new Tuple<int, double, double>(ticks, 0, 0)});
                        if (p > maxProb)
                        {
                            wait = ticks;
                            willSwing = true;
                            maxProb = p;
                        }
                    }
                }
                drawInfo.Enqueue((wait == Inf ? 0 : maxProb) + "");
                if (!willSwing && self.State == HockeyistState.Swinging)
                {
                    move.Action = ActionType.CancelStrike;
                }
                else if (willSwing && wait == 0 && self.State != HockeyistState.Swinging)
                {
                    move.Action = ActionType.Swing;
                }
                else if (wait == Inf)
                {
                    var to = GetStrikeFrom(new Point(self), GetSpeed(self), self.Angle);
                    if (self.GetDistanceTo(to.X, to.Y) < 200)
                        to = GetStrikePoint();
                    move.Turn = self.GetAngleTo(to.X, to.Y);
                    drawGoal2Queue.Enqueue(to);
                }
                else if (wait == 0)
                {
                    move.Action = ActionType.Strike;
                }
                else
                {
                    move.SpeedUp = selSpeedUp;
                    move.Turn = selTurn;
                }

                if (Math.Abs(move.Turn) > Deg(40))
                    move.SpeedUp = 0.2;
                else if (Math.Abs(move.Turn) > Deg(60))
                    move.SpeedUp = 0.05;
            }
            else
            {
                var owner = world.Hockeyists.FirstOrDefault(x => x.Id == puck.OwnerHockeyistId);

                if (puck.OwnerPlayerId == -1
                    && CanStrike(self, puck))
                {
                    move.Action = ActionType.TakePuck;
                }
                else if (puck.OwnerPlayerId == opp.Id
                         && (CanStrike(self, owner) || CanStrike(self, puck)))
                {
                    move.Action = ActionType.Strike;
                }
                else if (puck.OwnerPlayerId != my.Id
                         && CanStrike(self, puck)
                         && Strike(new Point(puck), GetSpeed(self), power, self.Angle))
                {
                    move.Action = ActionType.Strike;
                }
                else if (puck.OwnerPlayerId != self.PlayerId
                         && CanStrike(self, puck))
                {
                    var pk = new APuck(Get(puck), GetSpeed(puck), Get(myGoalie));
                    pk.IsDefend = true;
                    if (pk.Move(200) == 1)
                    {
                        move.Action = ActionType.Strike;
                    }
                    else
                    {
                        move.Action = ActionType.TakePuck;
                    }
                }
                else
                {
                    Point to = GetDefendPos2(new Point(self));
                    if (puck.OwnerPlayerId == my.Id ||
                        to.GetDistanceTo(self) < to.GetDistanceTo(friend)
                        )
                    {
                        StayOn(self, to, self.GetAngleTo(puck));
                    }
                    else
                    {
                        to = GoToPuck(new Point(self), new Point(self.SpeedX, self.SpeedY), self.Angle);
                        move.Turn = self.GetAngleTo(to.X, to.Y);
                    }
                    drawGoalQueue.Enqueue(new Point(to));
                }
            }
            if (Eq(move.SpeedUp, Inf))
                move.SpeedUp = 1;
#if DEBUG
            draw();
            Thread.Sleep(15);
#endif
            drawPathQueue.Clear();
            drawGoalQueue.Clear();
            drawGoal2Queue.Clear();
            drawInfo.Clear();
        }
    }
}