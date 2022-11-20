using Sandbox.Game.EntityComponents;
using Sandbox.ModAPI.Ingame;
using Sandbox.ModAPI.Interfaces;
using SpaceEngineers.Game.ModAPI.Ingame;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using VRage;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.GUI.TextPanel;
using VRage.Game.ModAPI.Ingame;
using VRage.Game.ModAPI.Ingame.Utilities;
using VRage.Game.ObjectBuilders.Definitions;
using VRageMath;
namespace IngameScript
{
    partial class Program : MyGridProgram
    {

        /*
        * Параметры
        * Stop - остановка стыковки и удаление точек стыковки
        * Pause - остановка стыковки без удаление точек
        * Любой параметр - 1 запуск:Добовляется в список точек стыковок
        *                  2 запуск:Запускает стыковку к заданой точке стыковки
        * Без параметров
        * Вывод Hello
        */

        

        const string cameraName = "Camera";
        const string RemoteControlName = "RemCon";
        const string TextPanel = "OWN LCD";

        List<IMyCockpit> Cockpits = new List<IMyCockpit>();
        IMyCockpit MainCockpit;
        List<IMyThrust> Thrusters = new List<IMyThrust>();
        List<IMyGyro> Gyros = new List<IMyGyro>();
        List<IMyShipConnector> Connectors = new List<IMyShipConnector>();
        WayPoint wayPointHome;
        List<WayPoint> waypoints = new List<WayPoint>();
        IMyTextSurface panel;
        double MinPower;

        IMyCameraBlock cameraForRaycast; // https://spaceengineerswiki.com/IMyCameraBlock/ru
        IMyRemoteControl remoteControl; // https://spaceengineerswiki.com/Remote_Control
        IMyTextPanel textPanel;

        VectorsCasting vc = new VectorsCasting();
        Vector3D DestinationWaypoint;

        double CubeGridRadius;

        Step step = Step.TakeDirectiont;
        public Program()
        {      
            step = Step.TakeDirectiont;
            cameraForRaycast = GridTerminalSystem.GetBlockWithName(cameraName) as IMyCameraBlock;
            cameraForRaycast.EnableRaycast = true;

            remoteControl = GridTerminalSystem.GetBlockWithName(RemoteControlName) as IMyRemoteControl;
            remoteControl.FlightMode = FlightMode.OneWay;
            remoteControl.Direction = Base6Directions.Direction.Forward;
            remoteControl.SetCollisionAvoidance(true);

            textPanel = GridTerminalSystem.GetBlockWithName(TextPanel) as IMyTextPanel;
            textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
          
            textPanel.WriteText("");
            CubeGridRadius = cameraForRaycast.CubeGrid.WorldVolume.Radius; 
        }
        private bool checkFreeWay(Vector3D DestinationWaypoint)
        {
            var detectedSomething = cameraForRaycast.Raycast(DestinationWaypoint);
            if (detectedSomething.IsEmpty())
            {
                FlyTo(DestinationWaypoint);
                return true;
            }
            else
            {
                var hitPos = (Vector3D)detectedSomething.HitPosition;
                SearchNFly(hitPos, DestinationWaypoint);
                return false;
            }
        }

        private void SearchNFly(Vector3D hitPos, Vector3D DestinationWaypoint)
        {
            var lengthToHitPos = (hitPos - cameraForRaycast.GetPosition()).Length();

            for (int i = 10; i < 1000; i += 10)
            {
                if (SpiralRaycast(cameraForRaycast, lengthToHitPos, i, cameraForRaycast.CubeGrid.WorldVolume.Radius))
                {
                    return;
                }
            }
            textPanel.WriteText("Error - no valid points, are you in pit/canyon?\n", true);
            throw new Exception("Error - no valid points, are you in pit/canyon?");
        }

        bool SpiralRaycast(IMyCameraBlock camera, double lengthToHit, int sizeInPoints, double distanceBtwPoints)
        {

            int i = 0;
            int j = 0;
            int direction = 0;// 0 - вправо, 1 - вверх, 2 - влево, 3 - вниз

            var Forward = new Vector3D(0, 0, -lengthToHit + CubeGridRadius);//минус - особенность worldMAtrix камеры
            var Up = new Vector3D(0, 1, 0);
            var Right = new Vector3D(1, 0, 0);

            int distanceBtwPointsInt = (int)distanceBtwPoints;
            int sizeInMeters = sizeInPoints * (int)distanceBtwPoints;

            Vector3D predNotEmptyDotHitPoint = cameraForRaycast.GetPosition();
            do
            {
                for (int k = 1; k <= (direction + 2) / 2; ++k)
                {
                    Right.X = i;
                    Up.Y = j;
                    var vec = vc.LocalToWorld(Forward + Right + Up, camera);
                    var ray = camera.Raycast(vec);

                    if (ray.IsEmpty())//TODO: возможно нужна проверка что dot между локальными векторами положительный
                    {
                        //TODO: возможно нужно из набора точек выбрать с максимальной дальностью от HitPos

                        //Проверка что расстояние от предидущего хитпоинта больше радиуса boundingBox грида
                        if ((predNotEmptyDotHitPoint - vec).Length() > CubeGridRadius * 5)
                        {
                            //textPanel.WriteText(vc.VectorToGPS(vec, $"R{Right.X} Up{Up.Y} - Летим сюда"), true);
                            //textPanel.WriteText("\n", true);
                            FlyTo(vec, "New Diretion");
                            return true;
                        }
                    }
                    else
                    {
                        predNotEmptyDotHitPoint = (Vector3D)ray.HitPosition;
                    }

                    switch (direction % 4)
                    {
                        case 0:
                            j += distanceBtwPointsInt;//вправо
                            break;
                        case 1:
                            i -= distanceBtwPointsInt;//вверх
                            break;
                        case 2:
                            j -= distanceBtwPointsInt;//влево
                            break;
                        case 3:
                            i += distanceBtwPointsInt;//вниз
                            break;
                    }
                }
                ++direction;

            } while (-sizeInMeters <= i && i <= sizeInMeters && -sizeInMeters <= j && j <= sizeInMeters);//пока не вышли за пределы
            return false;
        }


        private void FlyTo(Vector3D position, string waypointName = "AP Enabled")
        {
            remoteControl.ClearWaypoints();
            remoteControl.AddWaypoint(position, waypointName);
            remoteControl.SetAutoPilotEnabled(true);
        }

        bool ShipInProxyPoint(Vector3D DestinationWaypoint)
        { 
            if ((DestinationWaypoint - remoteControl.CubeGrid.GetPosition()).Length() < CubeGridRadius * 10)
            {
                remoteControl.SetAutoPilotEnabled(false);
                remoteControl.ClearWaypoints();
                return true;
            }
            return false;
        }

        enum Step
        {
            TakeDirectiont,
            CheckWay,
            ShipInPoint,
            Stop
        }
        public void Main_Fly(Vector3D argument)
        {
            switch (step)
            {

                case Step.TakeDirectiont:
                    DestinationWaypoint = argument;
                    DestinationWaypoint.Z += 30;
                    step = Step.CheckWay;
                    break;
                case Step.ShipInPoint:
                    if (ShipInProxyPoint(remoteControl.CurrentWaypoint.Coords))
                    {
                        step = Step.CheckWay;
                    }
                    break;
                case Step.CheckWay:
                    if (!checkFreeWay(DestinationWaypoint))
                    {
                        step = Step.ShipInPoint;
                    }
                    else if(ShipInProxyPoint(DestinationWaypoint))
                    {
                        step = Step.Stop;
                        Runtime.UpdateFrequency = UpdateFrequency.Update1;
                        Run();
                    }
                    break;
                default:
                    break;
            }
        }

        public class VectorsCasting
        {

            public Vector3D WorldToLocal(Vector3D worldCoordinats, IMyCubeBlock cubeBlock)
            {
                Vector3D mePosition = cubeBlock.CubeGrid.WorldMatrix.Translation;//also CubeGrid.GetPosition();
                Vector3D worldDirection = worldCoordinats - mePosition;
                return Vector3D.TransformNormal(worldDirection, MatrixD.Transpose(cubeBlock.CubeGrid.WorldMatrix));
            }

            public Vector3D LocalToWorldDirection(Vector3D local, IMyCubeBlock cubeBlock)
            {
                Vector3D world1Direction = Vector3D.TransformNormal(local, cubeBlock.CubeGrid.WorldMatrix);
                Vector3D worldPosition = cubeBlock.CubeGrid.WorldMatrix.Translation + world1Direction;
                return worldPosition;
            }
            public Vector3D LocalToWorld(Vector3D local, IMyCubeBlock cubeBlock)
            {
                return Vector3D.Transform(local, cubeBlock.WorldMatrix);
            }
            public Vector3D LocalToWorld(Vector3D local, IMyCubeGrid grid)
            {
                return Vector3D.Transform(local, grid.WorldMatrix);
            }
            public string VectorToGPS(Vector3D waypoint, string name = "NAVOTHER", string color = "#FF75C9F1")
            {
                return $"GPS:{name}:{waypoint.X}:{waypoint.Y}:{waypoint.Z}:{color}";
            }

            public Vector3D GPSToVector(string GPSCoordinats)
            {
                var strarr = GPSCoordinats.Split(':');
                var name = strarr[1];
                var x = double.Parse(strarr[2]);
                var y = double.Parse(strarr[3]);
                var z = double.Parse(strarr[4]);
                var vector = new Vector3D(x, y, z);
                return vector;
            }
        }
        
        public class WayPoint : IEquatable<WayPoint>
        {
            public WayPoint(string name, Vector3D position, Matrix matrix, int id)
            {
                Name = name; Position = position; Matrix = matrix; Id = id;
            }
            public string Name { get; set; }
            public Vector3D Position { get; set; }
            public Matrix Matrix { get; set; }
            public int Id { get; set; }
            public override bool Equals(object obj)
            {
                if (obj == null) return false;
                WayPoint objAsPart = obj as WayPoint;
                if (objAsPart == null) return false;
                else return Equals(objAsPart);
            }
            public bool Equals(WayPoint other)
            {
                if (other == null) return false;
                return (this.Id.Equals(other.Id));
            }
            public override int GetHashCode()
            {
                return Id;
            }
        }
        public void stop()
        {
            remoteControl.SetAutoPilotEnabled(false);
            remoteControl.ClearWaypoints();

            foreach (IMyThrust Thruster in Thrusters)
            {
                Thruster.ThrustOverridePercentage = 0f;
            }
            foreach (IMyGyro Gyro in Gyros)
            {
                Gyro.Pitch = 0;
                Gyro.Roll = 0;
                Gyro.Yaw = 0;
                Gyro.GyroOverride = false;
            }
            //Storage = "";
            MainCockpit.DampenersOverride = true;
            Runtime.UpdateFrequency = UpdateFrequency.None;
        }
        public void Run()
        {
            //textPanel.WriteText($"RUN {Connectors.Count > 0}\n");
            GyroAngl();
            ThrusterOverride();
            GridTerminalSystem.GetBlocksOfType<IMyShipConnector>(Connectors, (block) => (block.Status == MyShipConnectorStatus.Connectable));
            if (Connectors.Count > 0)
            {
                textPanel.WriteText($"Connected to {wayPointHome.Name}\n");
                Connectors[0].Connect();
                stop();
            }
        }
        public void Initialization()
        {
            GridTerminalSystem.GetBlocksOfType<IMyThrust>(Thrusters);
            GridTerminalSystem.GetBlocksOfType<IMyGyro>(Gyros);
            GridTerminalSystem.GetBlocksOfType<IMyCockpit>(Cockpits, (Cockpit) => Cockpit.CanControlShip && Cockpit.IsWorking);
            if (Cockpits.Count > 0)
                MainCockpit = Cockpits[0];
            else
                Echo("Cockpit not found");
            panel = MainCockpit.GetSurface(0);
            CalMinPower();
        }
        public void CalMinPower()
        {
            Matrix CockpitMatrix = new MatrixD();
            Matrix ThrusterMatrix = new MatrixD();

            MainCockpit.Orientation.GetMatrix(out CockpitMatrix);
            double[] powerThrs = { 0, 0, 0, 0, 0, 0 };
            /*double UpThrMax = 0;
            double DownThrMax = 1;
            double LeftThrMax = 2;
            double RightThrMax = 3;
            double ForwardThrMax = 4;
            double BackwardThrMax = 5;*/

            foreach (IMyThrust Thruster in Thrusters)
            {
                Thruster.Orientation.GetMatrix(out ThrusterMatrix);
                //Y
                if (ThrusterMatrix.Forward == CockpitMatrix.Up)
                {
                    powerThrs[0] += Thruster.MaxEffectiveThrust;
                }
                else if (ThrusterMatrix.Forward == CockpitMatrix.Down)
                {
                    powerThrs[1] += Thruster.MaxEffectiveThrust;
                }
                //X
                else if (ThrusterMatrix.Forward == CockpitMatrix.Left)
                {
                    powerThrs[2] += Thruster.MaxEffectiveThrust;
                }
                else if (ThrusterMatrix.Forward == CockpitMatrix.Right)
                {
                    powerThrs[3] += Thruster.MaxEffectiveThrust;
                }
                //Z
                else if (ThrusterMatrix.Forward == CockpitMatrix.Forward)
                {
                    powerThrs[4] += Thruster.MaxEffectiveThrust;
                }
                else if (ThrusterMatrix.Forward == CockpitMatrix.Backward)
                {
                    powerThrs[5] += Thruster.MaxEffectiveThrust;
                }
            }
            MinPower = powerThrs.Min();
        }
        public void ThrusterOverride()
        {
            Vector3D Gravity = Vector3D.Normalize(MainCockpit.GetNaturalGravity());
            Vector3D LinearVelocity = MainCockpit.GetShipVelocities().LinearVelocity;
            double ShipMass = MainCockpit.CalculateShipMass().PhysicalMass;
            double MinBoost = MinPower / ShipMass; //a = F/m минимальное ускорение коробля
            double TimeBraking = LinearVelocity.Length() / MinBoost; //t = u/a время торможения
            double BrakingWay = LinearVelocity.Length() * TimeBraking - ((MinBoost * (TimeBraking * TimeBraking)) / 2); //S = u*t-((a*t^2)/2) тормозной путь
            Vector3D VectorMove = LinearVelocity + Vector3D.Normalize(LinearVelocity) * BrakingWay;

            Vector3D VectorHome = MainCockpit.GetPosition() - wayPointHome.Position;

            if (Gravity.Length() > 0 && VectorMove.Length() > 1)
                VectorMove += Gravity;

            double ForwardThrust = (VectorHome + VectorMove).Dot(MainCockpit.WorldMatrix.Forward);
            double LeftThrust = (VectorHome + VectorMove).Dot(MainCockpit.WorldMatrix.Left);
            double UpThrust = (VectorHome + VectorMove).Dot(MainCockpit.WorldMatrix.Up);

            double BackwardThrust = -ForwardThrust;
            double RightThrust = -LeftThrust;
            double DownThrust = -UpThrust;

            Matrix CockpitMatrix = new MatrixD();
            Matrix ThrusterMatrix = new MatrixD();

            MainCockpit.Orientation.GetMatrix(out CockpitMatrix);
            foreach (IMyThrust Thruster in Thrusters)
            {
                Thruster.Orientation.GetMatrix(out ThrusterMatrix);
                //Y
                if (ThrusterMatrix.Forward == CockpitMatrix.Up)
                {
                    Thruster.ThrustOverride = (float)(UpThrust * Thruster.MaxEffectiveThrust);
                }
                else if (ThrusterMatrix.Forward == CockpitMatrix.Down)
                {
                    Thruster.ThrustOverride = (float)(DownThrust * Thruster.MaxEffectiveThrust);
                }
                //X
                else if (ThrusterMatrix.Forward == CockpitMatrix.Left)
                {
                    Thruster.ThrustOverride = (float)(LeftThrust * Thruster.MaxEffectiveThrust);
                }
                else if (ThrusterMatrix.Forward == CockpitMatrix.Right)
                {
                    Thruster.ThrustOverride = (float)(RightThrust * Thruster.MaxEffectiveThrust);
                }
                //Z
                else if (ThrusterMatrix.Forward == CockpitMatrix.Forward)
                {
                    Thruster.ThrustOverride = (float)(ForwardThrust * Thruster.MaxEffectiveThrust);
                }
                else if (ThrusterMatrix.Forward == CockpitMatrix.Backward)
                {
                    Thruster.ThrustOverride = (float)(BackwardThrust * Thruster.MaxEffectiveThrust);
                }
            }
        }
        public void GyroAngl()
        {
            foreach (IMyGyro Gyro in Gyros)
            {
                Gyro.GyroOverride = true;
                Vector3D axisForward = wayPointHome.Matrix.Forward.Cross(MainCockpit.WorldMatrix.Forward);
                Vector3D axisUp = wayPointHome.Matrix.Up.Cross(MainCockpit.WorldMatrix.Up);
                Vector3D axisLeft = wayPointHome.Matrix.Left.Cross(MainCockpit.WorldMatrix.Left);
                Vector3D axis = axisForward + axisUp + axisLeft;
                float Roll = (float)axis.Dot(Gyro.WorldMatrix.Backward);
                float Yaw = (float)axis.Dot(Gyro.WorldMatrix.Up);
                float Pitch = (float)axis.Dot(Gyro.WorldMatrix.Right);
                Gyro.Roll = Roll;
                Gyro.Pitch = Pitch;
                Gyro.Yaw = Yaw;
            }
        }

        public void Main(string argument, UpdateType uType)
        {
            if (uType == UpdateType.Update10)
            {
                Main_Fly(wayPointHome.Position);
           
            }else if (uType == UpdateType.Update1)
            {
                textPanel.WriteText($"Connecting to {wayPointHome.Name}\n");
                Run();
            }
            else
            {
                switch (argument)
                {
                    case "Stop":
                        stop();
                        waypoints = new List<WayPoint>();
                        break;
                    case "Pause":
                        step = Step.Stop;
                        stop();
                        break;
                    case "":
                        Echo("Hello");
                        break;
                    case null:
                        Echo("Hello");
                        break;
                    default:
                        Initialization();
                        if (waypoints.Exists(x => x.Name == argument))
                        {
                            wayPointHome = waypoints.Find(x => x.Name.Contains(argument));
                            Runtime.UpdateFrequency = UpdateFrequency.Update10;
                            textPanel.WriteText($"Fly to {argument}\n");
                            step = Step.TakeDirectiont;
                            Main_Fly(wayPointHome.Position);
                        }
                        else
                        {
                            Echo("added");
                            textPanel.WriteText($"Added point {argument}\n");
                            waypoints.Add(new WayPoint(argument, MainCockpit.GetPosition(), MainCockpit.WorldMatrix, waypoints.Count));
                        }
                        break;
                }

            }
        }
    }
}
