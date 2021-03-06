using System;
using System.Linq;
using System.Threading;
using Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk.Model;
using Point = Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk.Point;

namespace Com.CodeGame.CodeHockey2014.DevKit.CSharpCgdk 
{
    public partial class MyStrategy : IStrategy
    {
        public Puck puck;
        public Move move;
        public static Player Opp, My;
        public static Hockeyist OppGoalie, MyGoalie;
        public static World World;
        public static Game Game;
        
        public static double HoRadius, RinkWidth, RinkHeight, PuckRadius;
        public static Point RinkCenter;
        public static double HoPuckDist = 55.0;
        public static Hockeyist[] Hockeyists, MyRest;

        public int GetTicksToUp(AHock ho, Point to, double takePuck = -1, int limit = 500)
        {
            return GetTicksToUpN(ho.Clone(), to, takePuck, limit);
        }

        public int GetTicksToUpN(AHock ho, Point to, double takePuck = -1, int limit = 500)
        {
            var result = 0;
            for (; result < limit && (takePuck < 0 ? !CanStrike(ho, to) : ho.GetDistanceTo2(to) > takePuck*takePuck); result++)
            {
                ho.MoveTo(to);
            }
            return result;
        }

        public int GetTicksToDown(AHock ho, Point to, double takePuck = -1, int limit = 300)
        {
            return GetTicksToDownN(ho.Clone(), to, takePuck, limit);
        }

        public int GetTicksToDownN(AHock ho, Point to, double takePuck = -1, int limit = 300)
        {
            var result = 0;
            for (; result < limit && (takePuck < 0 ? !CanStrike(ho, to) : ho.GetDistanceTo2(to) > takePuck * takePuck); result++)
            {
                var turn = RevAngle(ho.GetAngleTo(to));
                var speedUp = -GetSpeedTo(turn);
                ho.Move(speedUp, TurnNorm(turn, ho.AAgility));
            }
            return result >= limit ? Inf : result;
        }

        public int MoveHockTo(AHock ho, Point to)
        {
            var result = 0;  
            for(; !CanStrike(ho, to); result++)
            {
                ho.MoveTo(to);

                if (result > 500)
                    return result;
            }
            return result;
        }

        public int GetTicksTo(Point to, Hockeyist my, bool tryDown = true)
        {
            var ho = new AHock(my);
            var up = GetTicksToUp(ho, to);
            var down = tryDown ? GetTicksToDown(ho, to) : Inf;
            if (up <= down)
                return up;
            return -down;
        }

        public Tuple<Point, int, int> GoToPuck(Hockeyist my, APuck pk, int ticksLimit = 300, bool tryDown = true)
        {
            if (my.Id == puck.OwnerHockeyistId)
                return new Tuple<Point, int, int>(null, 0, 0);

            if (ticksLimit == -1)
                ticksLimit = 300;

            const int noBs = 100;

            var res = Inf;
            var dir = 1; 
            var owner = Hockeyists.FirstOrDefault(x => x.Id == puck.OwnerHockeyistId);
            var ho = owner == null ? null : new AHock(owner);
            if (pk == null)
                pk = new APuck(puck, OppGoalie);
            else
                ho = null;

            var result = new Point(pk);
            int tLeft = 0, tRight = ticksLimit;
            var pks = new APuck[tRight + 1];
            var hhs = new AHock[tRight + 1];
            pks[0] = pk.Clone();
            hhs[0] = ho;
            for (var i = 1; i <= tRight; i++)
            {
                pks[i] = pks[i - 1].Clone();
                hhs[i] = ho == null ? null : hhs[i - 1].Clone();
                PuckMove(1, pks[i], hhs[i]);
            }
            while (ticksLimit > noBs && tLeft <= tRight)
            {
                var c = (tLeft + tRight)/2;
                var needTicks = GetTicksTo(PuckMove(0, pks[c], hhs[c]), my, tryDown);
                if (Math.Abs(needTicks) < c)
                {
                    tRight = c - 1;
                    res = c;
                    result = PuckMove(0, pks[c], hhs[c]);
                    dir = needTicks >= 0 ? 1 : -1;
                }
                else
                {
                    tLeft = c + 1;
                }
            }
            const int by = 10;
            for (var c = 0; c <= noBs && c <= ticksLimit; c += c < by ? 1 : by)
            {
                var needTicks = GetTicksTo(PuckMove(0, pks[c], hhs[c]), my, tryDown);
                if (Math.Abs(needTicks) <= c)
                {
                    for (var i = 0; i < by; i++, c--)
                    {
                        if (Math.Abs(needTicks) <= c)
                        {
                            res = c;
                            result = PuckMove(0, pks[c], hhs[c]);
                            dir = needTicks >= 0 ? 1 : -1;
                        }
                    }
                    break;
                }
            }
            return new Tuple<Point, int, int>(result, dir, res);
        }

        private static bool SubstSignal;
        private static Point _strikePoint;

        public void Move(Hockeyist self, World world, Game game, Move move)
        {
            if (self.State == HockeyistState.Resting)
                return;

            ShowWindow();

            // // fill globals
            _strikePoint = null;
            Hockeyists = world.Hockeyists;
            MyRest = Hockeyists.Where(x => x.IsTeammate && x.State == HockeyistState.Resting).ToArray();
            this.puck = world.Puck;
            this.move = move;
            World = world;
            Game = game;
            Opp = world.GetOpponentPlayer();
            My = world.GetMyPlayer();
            RinkWidth = game.RinkRight - game.RinkLeft;
            RinkHeight = game.RinkBottom - game.RinkTop;
            OppGoalie = Hockeyists.FirstOrDefault(x => !x.IsTeammate && x.Type == HockeyistType.Goalie);
            MyGoalie = Hockeyists.FirstOrDefault(x => x.IsTeammate && x.Type == HockeyistType.Goalie);
            HoRadius = self.Radius;
            RinkCenter = new Point(game.RinkLeft + RinkWidth/2, game.RinkTop + RinkHeight/2);
            PuckRadius = puck.Radius;
            var friends = Hockeyists
                .Where(x => x.IsTeammate && x.Id != self.Id && x.Type != HockeyistType.Goalie && x.State != HockeyistState.Resting)
                .ToArray();
            var friend1 = friends.Count() < 2 || friends[0].TeammateIndex < friends[1].TeammateIndex ? friends[0] : friends[1];
            var friend2 = friends.Count() > 1 ? friends[0].TeammateIndex < friends[1].TeammateIndex ? friends[1] : friends[0] : null;
            FillWayPoints();
            // //

            if (Game.OvertimeTickCount == 200) // ������� ����� ������ �����������
                return;

            TimerStart();

            var hock = new AHock(self);
            var needSubst = NeedTrySubstitute(hock);

            if (My.IsJustMissedGoal || My.IsJustScoredGoal)
            {
                SubstSignal = false;
                StayOn(self, GetSubstitutePoint(hock).First, null);
                TrySubstitute(hock);
            }
            else
            {
                var range = TurnRange(hock.AAgility);
                move.SpeedUp = Inf;
                if (self.State == HockeyistState.Swinging && self.Id != puck.OwnerHockeyistId)
                {
                    if (!TryStrikeWithoutTakeIfSwinging(hock, new APuck(puck, OppGoalie)))
                        move.Action = ActionType.CancelStrike;
                }
                else if (puck.OwnerHockeyistId == self.Id)
                {
                    var wait = Inf;
                    double selTurn = 0, selSpeedUp = 0;
                    var willSwing = false;
                    var maxProb = 0.15;
                    var selAction = ActionType.Strike;
                    TimerStart();
                    if (self.State != HockeyistState.Swinging)
                    {
                        var spUps = self.RemainingCooldownTicks == 0 || Math.Abs(self.X - My.NetFront) < RinkWidth / 2
                            ? (Math.Abs(self.X - Opp.NetFront) < RinkWidth / 3 ? new[] { 0.0, 1.0 } : new[] { 1.0 })
                            : new[] { 1.0, 0.5, 0.0, -0.5 };

                        var moveDirBase = MyRight() && self.Y > RinkCenter.Y || MyLeft() && self.Y < RinkCenter.Y ? 1 : -1;

                        // ���� �� ����������
                        for (var ticks = 0; ticks < 50; ticks++)
                        {
                            // ���� ���� ������������ (�� � �����!!!), �� ����� ��������� ������� game.SwingActionCooldownTicks

                            const int turns = 4;
                            for (var moveDir = -1; moveDir <= 1; moveDir += 2)
                            {
                                for (var moveTurn = 0.0; moveTurn - Eps <= range; moveTurn += range/turns)
                                {
                                    var turn = moveDir*moveTurn;
                                    foreach (var spUp in spUps)
                                    {
                                        if (moveDir == moveDirBase || spUp <= Eps && IsFinal())
                                        {
                                            var end = ticks + game.SwingActionCooldownTicks;
                                            var start = Math.Max(0, end - game.MaxEffectiveSwingTicks);
                                            // ����� �������� ������������
                                            var p = ProbabStrikeAfter(end - start, self, new[]
                                            {
                                                new MoveAction {Ticks = start, SpeedUp = spUp, Turn = turn},
                                                new MoveAction {Ticks = end - start, SpeedUp = 0, Turn = 0},
                                            }, ActionType.Strike);
                                            if (p > maxProb)
                                            {
                                                wait = start;
                                                willSwing = true;
                                                maxProb = p;
                                                selTurn = turn;
                                                selSpeedUp = spUp;
                                                selAction = ActionType.Strike;
                                            }

                                            // ���� �� ����
                                            p = ProbabStrikeAfter(0, self,
                                                new[] {new MoveAction {Ticks = ticks, SpeedUp = spUp, Turn = turn}},
                                                ActionType.Strike);
                                            if (p > maxProb)
                                            {
                                                wait = ticks;
                                                willSwing = false;
                                                maxProb = p;
                                                selTurn = turn;
                                                selSpeedUp = spUp;
                                                selAction = ActionType.Strike;
                                            }

                                            // ���� �����
                                            p = ProbabStrikeAfter(0, self,
                                                new[] {new MoveAction {Ticks = ticks, SpeedUp = spUp, Turn = turn}},
                                                ActionType.Pass);
                                            if (p > maxProb)
                                            {
                                                wait = ticks;
                                                willSwing = false;
                                                maxProb = p;
                                                selTurn = turn;
                                                selSpeedUp = spUp;
                                                selAction = ActionType.Pass;
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        // ���� ��� ����������
                        for (var ticks = Math.Max(0, game.SwingActionCooldownTicks - self.SwingTicks); ticks < 80; ticks++)
                        {
                            var p = ProbabStrikeAfter(ticks + self.SwingTicks, self,
                                new[] {new MoveAction {Ticks = ticks, SpeedUp = 0, Turn = 0}}, ActionType.Strike);
                            if (p > maxProb)
                            {
                                wait = ticks;
                                willSwing = true;
                                maxProb = p;
                                selAction = ActionType.Strike;
                            }
                        }
                    }
                    Log("STRIKE   " + TimerStop());
                    if (wait < Inf)
                    {
                        SubstSignal = true;
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
                        var wayPoint = FindWayPoint(self);
                        if (wayPoint == null)
                        {
                            needPassQueue.Enqueue(Get(self));
                            if (!TryPass(hock))
                            {
                                var pt = Math.Abs(Opp.NetFront - self.X) < RinkWidth/3
                                    ? Get(friend2 == null || (MyLeft() ? friend2.X > friend1.X : friend2.X < friend1.X) ? friend1 : friend2)
                                    : GetStrikePoint();
                                DoMove(self, pt, 1);
                            }
                        }
                        else
                        {
                            DoMove(self, wayPoint, 1);
                        }
                    }
                    else if (wait == 0)
                    {
                        move.Action = selAction;
                        if (selAction == ActionType.Pass)
                        {
                            move.PassPower = 1;
                            move.PassAngle = PassAngleNorm(hock.GetAngleTo(GetStrikePoint()));
                        }
                    }
                    else
                    {
                        move.SpeedUp = selSpeedUp;
                        move.Turn = selTurn;
                    }
                }
                else if (puck.OwnerPlayerId != -1 || !TryStrikeWithoutTake(hock, new APuck(puck, OppGoalie)))
                {
                    var owner = Hockeyists.FirstOrDefault(x => x.Id == puck.OwnerHockeyistId);
                    var pk = new APuck(puck, MyGoalie) {IsDefend = true};

                    if (puck.OwnerPlayerId == Opp.Id && (CanStrike(self, owner) || CanStrike(self, puck)))
                    { // ���������� ������
                        move.Action = ActionType.Strike;
                    }
                    else if (puck.OwnerPlayerId != self.PlayerId && CanStrike(self, puck))
                    {
                        // ��������� ��� �� ����� � ����� ������
                        var cpk = new APuck(puck, OppGoalie);
                        if (cpk.Move(200, true) == 0)
                        {
                            if (pk.Move(200, goalCheck: true) == 1) // ���� ������� �� �������
                                move.Action = ActionType.Strike;
                            else
                                move.Action = ActionType.TakePuck;
                        }
                    }
                    else
                    {
                        var toPuck = GoToPuck(self, null);
                        var toPuck1 = GoToPuck(friend1, null);
                        var toPuck2 = friend2 == null ? null : GoToPuck(friend2, null);
                        if (friend2 != null && toPuck1.Third < toPuck2.Third)
                        {
                            Swap(ref friend1, ref friend2);
                            Swap(ref toPuck1, ref toPuck2);
                        }
                        var def = GetDefendPos2();
                        var have = puck.OwnerPlayerId == My.Id;
                        // 1 - ������ ����� ���� �� �����
                        var net = new Point(My.NetFront, RinkCenter.Y);
                        double ii = net.GetDistanceTo(self) < 300 ? 1.0 : 1.0;
                        double jj = net.GetDistanceTo(friend1) < 300 ? 1.0 : 1.0;

                        var myFirst = GetFirstOnPuck(new[] {self},
                            new APuck(puck, OppGoalie),
                            true, 100, false).Second == (friend2 == null ? -1 : friend2.Id);

                        if (have
                            ? (friend2 == null || ii*GetTicksTo(def, self) < jj*GetTicksTo(def, friend1)) // ���� � �����, �� ��� �� ������
                            : toPuck.Third/ii > toPuck1.Third/jj) // ���� � ������ �����, �� ��� �� ������
                        {
                            if (needSubst && (SubstSignal || puck.OwnerPlayerId != Opp.Id && self.Y <= Game.RinkTop + 0.666 * RinkHeight || self.Y <= Game.SubstitutionAreaHeight + Game.RinkTop))
                            {
                                if (TrySubstitute(hock))
                                    SubstSignal = false;
                                else
                                    StayOn(self, GetSubstitutePoint(hock).First, null);
                            }
                            else
                            {
                                StayOn(self, def, Get(puck));
                            }
                        }
                            // ����� 1 ���� �� ������puck.OwnerPlayerId != My.Id
                        else if (friend2 == null 
                            || (puck.OwnerPlayerId != My.Id && (
                               toPuck.Third < toPuck2.Third  
                               //|| Math.Abs(Opp.NetFront - puck.X) < RinkWidth / 2 // ����� �� � ��� � �� ����� ��������
                               || !myFirst
                            )))
                        {
                            var bestTime = Inf;
                            double bestTurn = 0.0;
                            var needTime = GetFirstOnPuck(Hockeyists.Where(x => x.IsTeammate),
                                new APuck(puck, OppGoalie), true, -1).First;
                            var lookAt = new Point(Opp.NetFront, RinkCenter.Y);
                            for (var turn = -range; turn <= range; turn += range / 10)
                            {
                                var I = hock.Clone();
                                var P = new APuck(puck, OppGoalie);
                                for (var t = 0; t < needTime - 10 && t < 70; t++)
                                {
                                    if (CanStrike(I, P))
                                    {
                                        var cl = I.Clone();
                                        var tm = GetTicksToUp(cl, lookAt) + t;
                                        if (tm < bestTime)
                                        {
                                            bestTime = tm;
                                            bestTurn = turn;
                                        }
                                    }
                                    I.Move(0, turn);
                                    P.Move(1);
                                }
                            }
                            var i = hock.Clone();
                            var direct = MoveHockTo(i, toPuck.First);
                            direct += MoveHockTo(i, lookAt);
                            if (bestTime < direct && bestTime < Inf)
                            {
                                move.Turn = bestTurn;
                                move.SpeedUp = 0.0;
                            }
                            DoMove(self, toPuck.First, toPuck.Second);
                        }
                        else
                        {
                            if (needSubst && (SubstSignal || puck.OwnerPlayerId != Opp.Id && self.Y <= RinkCenter.Y || self.Y <= Game.SubstitutionAreaHeight + Game.RinkTop))
                            {
                                if (TrySubstitute(hock))
                                    SubstSignal = false;
                                else
                                    StayOn(self, GetSubstitutePoint(hock).First, null);
                            }
                            else
                            {
                                var c1 = new Point(RinkCenter.X, Game.RinkTop + 2*HoRadius);
                                var c2 = new Point(RinkCenter.X, Game.RinkBottom - 2*HoRadius);
                                var c = c1.GetDistanceTo(puck) > c2.GetDistanceTo(puck) ? c1 : c2;
                                var s = GetStrikePoint();
                                StayOn(self, c, s);
                            }
                        }
                    }
                }
                if (Eq(move.SpeedUp, Inf))
                    move.SpeedUp = 1;
            }

            Log(self.TeammateIndex + " >>>>>>>>>>>> " + TimerStop());
            if (move.Action != ActionType.None)
                Log(move.Action);
#if DEBUG
            draw();
            Thread.Sleep(8);
#endif
            drawInfo.Clear();
            needPassQueue.Clear();
        }
    }
}

// TODO: ? ����� ��� � ���� ��������� ����������� ������������
// TODO: ? ��� ������� - ��������
// TODO: ? ���� �� ��� ���� ���������