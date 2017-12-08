﻿using System.Linq;
using Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk.Model;

namespace Com.CodeGame.CodeWars2017.DevKit.CSharpCgdk
{
    public partial class MyStrategy
    {
        public static AMove FacilitiesStrategy()
        {
            if (MoveObserver.AvailableActions == 0)
                return null;
            if (Environment.Nuclears.Any(x => x.IsMy)) // лучше приберечь ходы
                return null;

            return _setFactoryProduction();
        }

        public static AMove ChangeFactoryProduction(AFacility factory)
        {
            VehicleType requiredType;

            var myFighters = Environment.GetVehicles(true, VehicleType.Fighter).Count;
            var oppFighters = Environment.GetVehicles(false, VehicleType.Fighter).Count;

            var nearest = Environment.OppVehicles.ArgMin(x => x.GetDistanceTo2(factory.Center));
            if (factory.Center.GetDistanceTo(nearest) < 200 && nearest.Type == VehicleType.Fighter)
            {
                requiredType = VehicleType.Ifv;
            }
            else if (factory.Center.GetDistanceTo(nearest) < 200 && nearest.Type == VehicleType.Ifv)
            {
                requiredType = VehicleType.Tank;
            }
            else if (myFighters < oppFighters)
            {
                requiredType = VehicleType.Fighter;
            }
            else if (oppFighters < 10 && Environment.MyVehicleFactories.Count(x => x.VehicleType == VehicleType.Helicopter) - (factory.VehicleType == VehicleType.Helicopter ? 1 : 0) == 0)
            {
                requiredType = VehicleType.Helicopter;
            }
            else
            {
                requiredType = VehicleType.Tank;
            }

            if (requiredType == factory.VehicleType)
                return null;

            return new AMove
            {
                Action = ActionType.SetupVehicleProduction,
                VehicleType = requiredType,
                FacilityId = factory.Id,
            };
        }

        private static AMove _setFactoryProduction()
        {
            var newFactories = Environment.MyVehicleFactories.Where(f => f.VehicleType == null).ToArray();
            if (newFactories.Length > 0)
                return ChangeFactoryProduction(newFactories[0]);

            var factoryToChange = Environment.MyVehicleFactories.FirstOrDefault(
                f => f.VehicleType != null && MyUngroupedClusters.All(cl => !f.ContainsPoint(cl[0])));

            if (factoryToChange != null)
                return ChangeFactoryProduction(factoryToChange);

            return null;
        }
    }
}
