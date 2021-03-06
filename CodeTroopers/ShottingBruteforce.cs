﻿using System;
using System.Collections.Generic;
using System.Diagnostics.Eventing.Reader;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk.Model;
using System.Collections;
using System.IO;

namespace Com.CodeGame.CodeTroopers2013.DevKit.CSharpCgdk
{
    public partial class MyStrategy : IStrategy
    {
        private static Trooper[] Troopers;
        private static int counter = 0;
        private static int OpponentsCount;
        private static Trooper OpponentCommander; // работает для игры 2x5
        private static int MyCount;
        private static double[] probab;

        private class State
        {
            public int[] stance;
            public int[] hit;
            public int[] act;
            public Point[] Position;
            public int id;
            public double profit;
            public bool[] medikit;
            public bool[] grenade;
            public int[] opphit;

            public int Stance
            {
                get
                {
                    return stance[id];
                }
                set
                {
                    stance[id] = value;
                }
            }

            public bool Grenade
            {
                get
                {
                    return grenade[id];
                }
                set
                {
                    grenade[id] = value;
                }
            }

            public bool Medikit
            {
                get
                {
                    return medikit[id];
                }
                set
                {
                    medikit[id] = value;
                }
            }

            public int X
            {
                get
                {
                    return Position[id].X;
                }
                set
                {
                    Position[id].X = value;
                }
            }

            public int Y
            {
                set
                {
                    Position[id].Y = value;
                }
                get
                {
                    return Position[id].Y;
                }
            }

            public override string ToString()
            {
                return "" + id + " " + Position[id];
            }
        }

        private static int CommanderId; // индекс Commandera, или -1 если его нет
        private static State state;
        private static ArrayList[] stack;
        private static ArrayList[] bestStack;
        private static double bestProfit;
        private static int Multiplier;

        private static void StackPush(object obj)
        {
            stack[state.id].Add(obj);
        }

        private static void StackPop()
        {
            stack[state.id].RemoveAt(stack[state.id].Count - 1);
        }

        private void dfs_changeStance1(bool allowShot = true)
        {
            var id = state.id;

            const int upper = 2, lower = -2;
            //for (var deltaStance = lower; deltaStance <= upper; deltaStance++)
            for (var deltaStance = upper; deltaStance >= lower; deltaStance--)
            {
                // Изменяю stance на deltaStance
                state.Stance += deltaStance;
                if (state.Stance >= 0 && state.Stance < 3)
                {
                    var cost = game.StanceChangeCost * Math.Abs(deltaStance);
                    if (cost <= state.act[id])
                    {
                        state.act[id] -= cost;
                        if (deltaStance != 0)
                            StackPush("st " + deltaStance);
                        // Отсечение: после того как сел - нет смысла идти
                        dfs_move(deltaStance < 0 ? 0 : 5, true, allowShot);
                        if (deltaStance != 0)
                            StackPop();
                        state.act[id] += cost;
                    }
                }
                state.Stance -= deltaStance;
            }
        }

        private void dfs_move(int bfsRadius, bool mayHeal = true, bool allowShot = true)
        {
            var id = state.id;
            var stance = state.Stance;
            var moveCost = GetMoveCost(stance);

            var encircling = FastBfs(state.X, state.Y, bfsRadius, notFilledMap, state.Position);
            foreach (Point to in encircling)
            {
                var byX = to.X - state.X;
                var byY = to.Y - state.Y;
                state.X += byX;
                state.Y += byY;
                var cost = (int)(to.profit + Eps)*moveCost;
                if (cost <= state.act[id] && Validate(state))
                {
                    state.act[id] -= cost;
                    if (byX != 0 || byY != 0)
                        StackPush("at " + state.X + " " + state.Y);
                    if (allowShot)
                    {
                        dfs_grenade();
                        if (mayHeal)
                            dfs_heal();
                    }
                    dfs_changeStance5(allowShot);
                    if (byX != 0 || byY != 0)
                        StackPop();
                    state.act[id] += cost;
                }
                state.X -= byX;
                state.Y -= byY;
            }
        }

        private void dfs_heal()
        {
            var id = state.id;

            if (state.Medikit && game.MedikitUseCost < state.act[id])
            {
                state.Medikit = false;
                for (var i = 0; i < MyCount; i++)
                {
                    var to = state.Position[i];
                    // если имеет смысл юзать, и если он рядом
                    if (state.hit[i] < 0.8*Troopers[i].MaximalHitpoints && state.Position[id].Nearest(to))
                    {
                        var oldhit = state.hit[i];
                        state.act[id] -= game.MedikitUseCost;
                        state.hit[i] = Math.Min(Troopers[i].MaximalHitpoints, state.hit[i] + (i == id ? game.MedikitHealSelfBonusHitpoints : game.MedikitBonusHitpoints));
                        StackPush("med " + to.X + " " + to.Y);
                        dfs_move(5);
                        StackPop();
                        state.hit[i] = oldhit;
                        state.act[id] += game.MedikitUseCost;
                    }
                }
                state.Medikit = true;
            }
            else if (Troopers[id].Type == TrooperType.FieldMedic)
            {
                for (var i = 0; i < MyCount; i++)
                {
                    var to = state.Position[i];
                    // если он рядом
                    if (state.hit[i] < Troopers[i].MaximalHitpoints && state.Position[id].Nearest(to))
                    {
                        var can = state.act[id]/game.FieldMedicHealCost;
                        var by = i == id ? game.FieldMedicHealSelfBonusHitpoints : game.FieldMedicHealBonusHitpoints;
                        var need = Math.Min(can, (Troopers[i].MaximalHitpoints - state.hit[i] + by - 1) / by);
                        var cost = need*game.FieldMedicHealCost;
                        var healBy = by*need;

                        var oldhit = state.hit[i];
                        state.act[id] -= cost;
                        state.hit[i] = Math.Min(Troopers[i].MaximalHitpoints, state.hit[i] + healBy);
                        StackPush("heal " + to.X + " " + to.Y + " " + need);
                        dfs_move(5, false);
                        StackPop();
                        state.hit[i] = oldhit;
                        state.act[id] += cost;
                    }
                }
            }
        }

        private void dfs_grenade()
        {
            var id = state.id;

            if (state.Grenade && state.act[id] >= game.GrenadeThrowCost)
            {
                var bestPoint = Point.MInf;
                foreach (var trooper in Opponents)
                {
                    for (var k = 0; k < 5; k++)
                    {
                        var ni = trooper.X + _i_[k];
                        var nj = trooper.Y + _j_[k];
                        if (ni >= 0 && nj >= 0 && ni < Width && nj < Height && notFilledMap[ni, nj] == 0
                            && state.Position[id].GetDistanceTo(ni, nj) <= game.GrenadeThrowRange)
                        {
                            var profit = 0;
                            var loop = 0;
                            foreach (var tr in Opponents)
                            {
                                var distance = Math.Abs(ni - tr.X) + Math.Abs(nj - tr.Y);
                                if (distance <= 1)
                                {
                                    var damage = distance == 0 ? game.GrenadeDirectDamage : game.GrenadeCollateralDamage;
                                    profit += Math.Min(state.opphit[loop], damage);
                                    if (damage >= state.opphit[loop])
                                        profit += KillBonus;
                                }
                                loop++;
                            }
                            if (bestPoint.profit < profit)
                                bestPoint.Set(ni, nj, profit);
                        }
                    }
                }
                if (bestPoint.profit >= game.GrenadeCollateralDamage)
                {
                    var loop = 0;
                    var Damage = new int[OpponentsCount];
                    for (var i = 0; i < OpponentsCount; i++)
                    {
                        var opp = Opponents[i];
                        var dist = Math.Abs(bestPoint.X - opp.X) + Math.Abs(bestPoint.Y - opp.Y);
                        if (dist == 0)
                        {
                            Damage[i] = Math.Min(state.opphit[loop], game.GrenadeDirectDamage);
                            state.opphit[i] -= Damage[i];
                        }
                        else if (dist == 1)
                        {
                            Damage[i] = Math.Min(state.opphit[loop], game.GrenadeCollateralDamage);
                            state.opphit[i] -= Damage[i];
                        }
                        loop++;
                    }
                    state.Grenade = false;
                    StackPush("gr " + bestPoint.X + " " + bestPoint.Y);
                    state.act[id] -= game.GrenadeThrowCost;
                    state.profit += bestPoint.profit;
                    dfs_move(5);
                    state.profit -= bestPoint.profit;
                    state.act[id] += game.GrenadeThrowCost;
                    StackPop();
                    state.grenade[id] = true;
                    for (var i = 0; i < OpponentsCount; i++)
                        state.opphit[i] += Damage[i];
                }
            }
        }

        void dfs_changeStance5(bool allowShot = true)
        {
            var id = state.id;

            const int upper = 2, lower = -2;
            for (var deltaStance = upper; deltaStance >= lower; deltaStance--)
            //for (var deltaStance = lower; deltaStance <= upper; deltaStance++)
            {
                state.Stance += deltaStance;
                if (state.Stance >= 0 && state.Stance < 3)
                {
                    var cost = game.StanceChangeCost * Math.Abs(deltaStance);
                    if (cost <= state.act[id])
                    {
                        state.act[id] -= cost;
                        if (deltaStance != 0)
                            StackPush("st " + deltaStance);
                        if (allowShot)
                            dfs_shot();
                        dfs_end();
                        if (deltaStance != 0)
                            StackPop();
                        state.act[id] += cost;
                    }
                }
                state.Stance -= deltaStance;
            }
        }

        private void dfs_shot()
        {
            var id = state.id;

            var can = state.act[id] / Troopers[id].ShootCost;
            var damage = Troopers[id].GetDamage(GetStance(state.Stance));

            double maxProbab = 0.0;
            for (var i = 0; i < OpponentsCount; i++)
            {
                var opp = Opponents[i];
                if (probab[i] > maxProbab
                    && state.opphit[i] > 0
                    && world.IsVisible(GetShootingRange(Troopers[id], state.Stance), state.X, state.Y, GetStance(state.Stance), opp.X, opp.Y, opp.Stance)
                   )
                    maxProbab = probab[i];
            }

            for(var bestIdx = 0; bestIdx < OpponentsCount; bestIdx++)
            {
                var oldOppHit = state.opphit[bestIdx];
                var oldProfit = state.profit;
                var opp = Opponents[bestIdx];

                if (oldOppHit > 0 // если он ещё жив
                    && EqualF(probab[bestIdx], maxProbab)
                    && world.IsVisible(GetShootingRange(Troopers[id], state.Stance), state.X, state.Y, GetStance(state.Stance), opp.X, opp.Y, opp.Stance)
                    )
                {
                    state.profit += GetShootingPriority(opp)*0.001;
                    var need = Math.Min(can, (oldOppHit + damage - 1)/damage);
                    for (var cnt = 1; cnt <= need; cnt++)
                    {
                        // cnt - сколько выстрелов сделаю
                        var p = damage*cnt;
                        state.opphit[bestIdx] = Math.Max(0, oldOppHit - p);
                        state.profit += id == 0 ? p*1.2 : p;
                        if (oldOppHit - p <= 0)
                            state.profit += KillBonus;
                        state.act[id] -= cnt*Troopers[id].ShootCost;
                        var oldStackSize = stack[id].Count;
                        for (var k = 0; k < cnt; k++)
                            StackPush("sh " + opp.X + " " + opp.Y);
                        dfs_changeStance1(allowShot: false);
                        stack[id].RemoveRange(oldStackSize, stack[id].Count - oldStackSize);
                        state.act[id] += cnt*Troopers[id].ShootCost;
                        state.profit = oldProfit;
                        state.opphit[bestIdx] = oldOppHit;
                    }
                }
            }
        }

        public Player GetPlayer(long id)
        {
            return world.Players.FirstOrDefault(player => player.Id == id);
        }

        private double getDanger(Trooper opp, int x, int y, int st, int h)
        {
            if (GetPlayer(opp.PlayerId) != null && GetPlayer(opp.PlayerId).IsStrategyCrashed)
                return 0;
            // Чтобы не бояться одного снайпера
            var oppShootingRange = state.opphit.Count(e => e > 0) == 1 && opp.Type == TrooperType.Sniper
                ? GetVisionRange(opp, Troopers[state.id], state.Stance)
                : GetShootingRange(opp, opp.Stance);
            int d = GetDistanceToShoot((int)(oppShootingRange + Eps), opp.X, opp.Y, GetStanceId(opp.Stance), x, y, st);
            // если он достреливает сразу:
            if (d == 0)
            {
                return 200;
            }

            int v;
            if (world.IsVisible(GetVisionRange(opp, Troopers[state.id], state.Stance), opp.X, opp.Y, opp.Stance, x, y, GetStance(state.Stance)))
                v = 0;
            else
                v = GetDistanceTo((int)(GetVisionRange(opp, Troopers[state.id], state.Stance) + Eps),
                    opp.X, opp.Y, GetStanceId(opp.Stance), x, y, st);

            double visiblePenalty = 50 * (v == 0 ? 1 : Math.Exp(-v));

            var oppAct = opp.InitialActionPoints;
            if (opp.Type != TrooperType.Scout && opp.Type != TrooperType.Soldier && OpponentCommander != null && OpponentCommander.GetDistanceTo(opp) <= game.CommanderAuraRange)
                oppAct += game.CommanderAuraBonusActionPoints;

            // если он подходит и убивает
            int act = oppAct - d * 2; // TODO: ???
            int can = act / opp.ShootCost;
            double dam = can * opp.StandingDamage;
            if (dam >= h)
                return 150 + visiblePenalty;

            // если он подходит и отнимает жизни
            // тогда теперь он вынужден отбегать
            act = oppAct - (d * 2) * 2; // TODO: ???
            can = act / opp.ShootCost;
            dam = Math.Max(0, can * opp.StandingDamage) + visiblePenalty;
            if (dam <= 0)
                return 0;
            return dam;
        }

        void dfs_end()
        {
            var id = state.id;
            double profit = state.profit;

            double centerX = 0, centerY = 0;
            foreach (var position in state.Position)
            {
                centerX += position.X;
                centerY += position.Y;
            }
            centerX /= MyCount;
            centerY /= MyCount;
            for (var i = 0; i < MyCount; i++)
            {
                // бонус за то что может стрелять на следующем ходу
                if (HowManyCanShoot(state.Position[i], GetStance(state.stance[i])) != 0)
                    profit += 3;
                // штраф за то что далеко от команды
                var distance = state.Position[i].GetDistanceTo(centerX, centerY);
                if (distance > MaxTeamRadius)
                    profit -= distance * 0.01;
                // бонус за близость к ближайшему врагу - ???
                if (Troopers[i].Type != TrooperType.Sniper)
                {
                    if (Troopers[i].Type == TrooperType.Scout)
                        profit -= CellDanger[state.Position[i].X, state.Position[i].Y, state.stance[i]];
                    double minDist = Inf;
                    foreach (var opp in Opponents)
                    {
                        minDist = Math.Min(minDist, state.Position[i].GetDistanceTo(opp));
                    }
                    profit -= minDist * 0.1;
                }
            }

            var oldHit = state.hit[id];
            for (var i = 0; i < OpponentsCount; i++)
            {
                if (state.opphit[i] > 0)
                {
                    profit -= (int)getDanger(Opponents[i], state.X, state.Y, state.Stance, state.hit[id]) * Multiplier;
                }
            }

            if (id == 1 || id == MyCount - 1)
            {
                // К профиту прибавляю итоговое количество жизней
                for (var i = 0; i < MyCount; i++)
                    profit += state.hit[i] * (Troopers[i].Type == TrooperType.FieldMedic ? 1 : Multiplier);

                // counter - количество состояний - для дебага
                counter++;
                if (profit > bestProfit)
                {
                    bestProfit = profit;
                    bestStack = new ArrayList[MyCount];
                    for (var i = 0; i < MyCount; i++)
                        bestStack[i] = stack[i].Clone() as ArrayList;
                }
            }
            else
            {
                // EndTurn
                var oldProfit = state.profit;
                state.profit = profit;
                var prevId = state.id;
                state.id = (state.id + 1)%MyCount;
                dfs_changeStance1();
                state.profit = oldProfit;
                state.id = prevId;
            }
            state.hit[id] = oldHit;
        }


        public int GetShootingPriority(Trooper opponent)
        {
            for(var i = 0; i < ShootingPriority.Length; i++)
                if (opponent.Type == ShootingPriority[i])
                    return i;
            throw new InvalidDataException();
        }

        int getInitialActionPoints(Trooper tr)
        {
            var points = tr.InitialActionPoints;
            if (CommanderId != -1 && tr.GetDistanceTo(state.Position[CommanderId].X, state.Position[CommanderId].Y) <= game.CommanderAuraRange)
                points += game.CommanderAuraBonusActionPoints;
            return points;
        }

        private bool Validate(State state)
        {
            if (!(state.X >= 0 && state.Y >= 0 && state.X < Width && state.Y < Height && notFilledMap[state.X, state.Y] == 0))
                return false;
            // Проверяю чтобы не стать в занятую клетку
            return state.Position.Count(p => p.X == state.X && p.Y == state.Y) < 2;
        }

        Move BruteForceDo()
        {
            var fictive = 0;
            if (queue.Count < Team.Count())
            {
                foreach (var tr in Team)
                {
                    if (!queue.Contains(tr.Id))
                    {
                        queue.Add(tr.Id);
                        fictive++;
                    }
                }
            }

            OpponentCommander = Opponents.FirstOrDefault(opp => opp.Type == TrooperType.Commander);
            state = new State();
            state.Position = new Point[Team.Count()];
            state.stance = new int[Team.Count()];
            state.act = new int[Team.Count()];
            state.hit = new int[Team.Count()];
            state.medikit = new bool[Team.Count()];
            state.grenade = new bool[Team.Count()];
            Troopers = new Trooper[Team.Count()];
            CommanderId = -1;
            foreach (var tr in troopers)
            {
                if (tr.IsTeammate)
                {
                    int pos = GetQueuePlace2(tr, true) - 1;
                    state.Position[pos] = new Point(tr);
                    state.stance[pos] = GetStanceId(tr.Stance);
                    state.medikit[pos] = tr.IsHoldingMedikit;
                    state.grenade[pos] = tr.IsHoldingGrenade;
                    Troopers[pos] = tr;
                    if (tr.Type == TrooperType.Commander)
                        CommanderId = pos;
                }
            }
            state.id = 0;
            state.profit = 0;
            state.act[0] = Troopers[0].ActionPoints;
            MyCount = state.Position.Count();
            for(var i = 0; i < MyCount; i++)
                state.hit[i] = Troopers[i].Hitpoints;
            for (var i = 1; i < Troopers.Count(); i++)
                state.act[i] = getInitialActionPoints(Troopers[i]);
            stack = new ArrayList[MyCount];
            bestStack = new ArrayList[MyCount];
            for (var i = 0; i < MyCount; i++)
            {
                stack[i] = new ArrayList();
                bestStack[i] = null;
            }
            bestProfit = -Inf;
            counter = 0;
            OpponentsCount = Opponents.Count();
            MyCount = state.Position.Count();
            Multiplier = Math.Min(MyCount, 3);
            state.opphit = new int[OpponentsCount];
            probab = new double[OpponentsCount];
            for (var i = 0; i < probab.Length; i++)
                probab[i] = 1.0;
            for (var i = 0; i < OpponentsCount; i++)
            {
                state.opphit[i] = Opponents[i].Hitpoints;
                // Чтобы уменьшить приоритет стрельбы в "мнимую" цель
                if (world.MoveIndex - OpponentsMemoryAppearTime[i] > 1)
                    probab[i] /= 2;
                else if (OpponentsMemoryType[i] == self.Type && world.MoveIndex - OpponentsMemoryAppearTime[i] == 1)
                    probab[i] /= 2;
                    // TODO: можно точнее
                else if (!(OpponentsMemoryType[i] == self.Type || IsBetween(OpponentsMemoryType[i], self.Type, Opponents[i].Type)))
                    probab[i] /= 2;
            }
            dfs_changeStance1();

            // remove fictive from queue
            queue.RemoveRange(queue.Count - fictive, fictive);

            var move = new Move();
            ReduceStack(bestStack[0]);
            if (bestStack[0].Count == 0)
            {
                // EndTurn
                bestStack[0].Add("at " + self.X + " " + self.Y);
            }
            var cmd = ((string)bestStack[0][0]).Split(' ');
            if (cmd[0] == "st")
            {
                // Change stance
                var ds = int.Parse(cmd[1]);
                if (ds < 0)
                    move.Action = ActionType.LowerStance;
                else if (ds > 0)
                    move.Action = ActionType.RaiseStance;
                else
                    throw new InvalidDataException();
            }
            else if (cmd[0] == "at")
            {
                var x = int.Parse(cmd[1]);
                var y = int.Parse(cmd[2]);
                var to = bestStack[0].Count == 1
                    ? GoScouting(new Point(x, y), new Point(Opponents[0]), changeStanceAllow: true)
                    : GoToUnit(self, new Point(x, y), map, beginFree: true, endFree: false);
                if (to.X == -1)
                    move.Action = to.Y == -1 ? ActionType.LowerStance : ActionType.RaiseStance;
                else
                {
                    move.Action = ActionType.Move;
                    move.X = to.X;
                    move.Y = to.Y;
                }
            }
            else if (cmd[0] == "sh")
            {
                var x = int.Parse(cmd[1]);
                var y = int.Parse(cmd[2]);
                move.Action = ActionType.Shoot;
                move.X = x;
                move.Y = y;
            }
            else if (cmd[0] == "med")
            {
                var to = new Point(int.Parse(cmd[1]), int.Parse(cmd[2]));
                move.Action = ActionType.UseMedikit;
                move.X = to.X;
                move.Y = to.Y;
            }
            else if (cmd[0] == "gr")
            {
                var to = new Point(int.Parse(cmd[1]), int.Parse(cmd[2]));
                move.Action = ActionType.ThrowGrenade;
                move.X = to.X;
                move.Y = to.Y;
            }
            else if (cmd[0] == "heal")
            {
                var to = new Point(int.Parse(cmd[1]), int.Parse(cmd[2]));
                move.Action = ActionType.Heal;
                move.X = to.X;
                move.Y = to.Y;
            }
            else
            {
                throw new NotImplementedException(cmd.ToString());
            }
            return move;
        }

        private void ReduceStack(ArrayList stack)
        {
            for (var i = 1; i < stack.Count; i++)
            {
                var prev = (string) stack[i - 1];
                var cur = (string)stack[i];
                if (prev.StartsWith("st") && cur.StartsWith("st"))
                {
                    var a = int.Parse(prev.Split(' ')[1]);
                    var b = int.Parse(cur.Split(' ')[1]);
                    stack.RemoveRange(i - 1, 2);
                    if (a + b != 0)
                        stack.Insert(i - 1, "st " + (a + b));
                    i--;
                }
            }
        }
    }
}
