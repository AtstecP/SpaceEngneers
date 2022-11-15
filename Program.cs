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
        // This file contains your actual script.
        //
        // You can either keep all your code here, or you can create separate
        // code files to make your program easier to navigate while coding.
        //
        // In order to add a new utility class, right-click on your project, 
        // select 'New' then 'Add Item...'. Now find the 'Space Engineers'
        // category under 'Visual C# Items' on the left hand side, and select
        // 'Utility Class' in the main area. Name it in the box below, and
        // press OK. This utility class will be merged in with your code when
        // deploying your final script.
        //
        // You can also simply create a new utility class manually, you don't
        // have to use the template if you don't want to. Just do so the first
        // time to see what a utility class looks like.
        // 
        // Go to:
        // https://github.com/malware-dev/MDK-SE/wiki/Quick-Introduction-to-Space-Engineers-Ingame-Scripts
        //
        // to learn more about ingame scripts.
        const string cameraName = "Camera";
        const string RemoteControlName = "RemCon";
        const string TextPanel = "OWN LCD";

        IMyCameraBlock cameraForRaycast; // https://spaceengineerswiki.com/IMyCameraBlock/ru
        IMyRemoteControl remoteControl; // https://spaceengineerswiki.com/Remote_Control
        IMyTextPanel textPanel;

        VectorsCasting vc = new VectorsCasting();
        Vector3D DestinationWaypoint;


        private List<IMyGyro> gyros;
        private bool isGyroOver = false;

        private double CubeGridRadius;
        public Program()
        {
            // The constructor, called only once every session and
            // always before any other method is called. Use it to
            // initialize your script. 
            //     
            // The constructor is optional and can be removed if not
            // needed.
            // 
            // It's recommended to set Runtime.UpdateFrequency 
            // here, which will allow your script to run itself without a 
            // timer block.
            cameraForRaycast = GridTerminalSystem.GetBlockWithName(cameraName) as IMyCameraBlock;
            cameraForRaycast.EnableRaycast = true;

            remoteControl = GridTerminalSystem.GetBlockWithName(RemoteControlName) as IMyRemoteControl;
            remoteControl.FlightMode = FlightMode.OneWay;
            remoteControl.Direction = Base6Directions.Direction.Forward;
            remoteControl.SetCollisionAvoidance(true);

            textPanel = GridTerminalSystem.GetBlockWithName(TextPanel) as IMyTextPanel;
            textPanel.ContentType = ContentType.TEXT_AND_IMAGE;
            textPanel.WriteText("");

            gyros = new List<IMyGyro>();
            GridTerminalSystem.GetBlocksOfType(gyros, gyro => gyro.IsSameConstructAs(remoteControl));

            CubeGridRadius = cameraForRaycast.CubeGrid.WorldVolume.Radius; // Предпположительно радиус сферы (объема мира) 
        }

        private bool checkFreeWay(Vector3D DestinationWaypoint)
        {
            var detectedSomething = cameraForRaycast.Raycast(DestinationWaypoint);
            if (detectedSomething.IsEmpty())
            {
                textPanel.WriteText("Fly...\n", true);
                FlyTo(DestinationWaypoint);
                return true;
            }
            else
            {
                textPanel.WriteText("Find...\n", true);
                //Сюда алгоритм нахождения иной точки
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
            //Начиная с центра, обойти по спирали все элементы квадратной матрицы,
            //выводя их в порядке обхода.
            //Обход выполнять против часовой стрелки, первый ход - вправо.
            int i = 0;
            int j = 0;
            int direction = 0;// 0 - вправо, 1 - вверх, 2 - влево, 3 - вниз

            //var Forward = vc.WorldToLocal(DestinationWaypoint,camera);
            //Forward.Z = -lengthToHit + CubeGridRadius;
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
                    //textPanel.WriteText(vc.VectorToGPS(vec, counter++.ToString()) + "\n", true);

                    var ray = camera.Raycast(vec);

                    if (ray.IsEmpty())//TODO: возможно нужна проверка что dot между локальными векторами положительный
                    {
                        //TODO: возможно нужно из набора точек выбрать с максимальной дальностью от HitPos

                        //Проверка что расстояние от предидущего хитпоинта больше радиуса boundingBox грида
                        if ((predNotEmptyDotHitPoint - vec).Length() > CubeGridRadius * 5)
                        {
                            textPanel.WriteText(vc.VectorToGPS(vec, $"R{Right.X} Up{Up.Y} - Летим сюда"), true);
                            textPanel.WriteText("\n", true);
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
            //remoteControl.CubeGrid.WorldAABB.Distance()
            if ((DestinationWaypoint - remoteControl.CubeGrid.GetPosition()).Length() < CubeGridRadius * 3)
                //((remoteControl.CurrentWaypoint.Coords - remoteControl.CubeGrid.GetPosition()).Length() < CubeGridRadius * 3)
            {
                remoteControl.SetAutoPilotEnabled(false);
                remoteControl.ClearWaypoints();
                return true;
            }
            return false;
        }

        private void KeepDirectionTo(Vector3D pointToSeeWorld)
        {
            textPanel.WriteText("keep dir", true);
            Vector3D horizontalDirection = pointToSeeWorld.Cross(remoteControl.WorldMatrix.Forward);
            if (pointToSeeWorld.Dot(remoteControl.WorldMatrix.Forward) < 0)
            {
                horizontalDirection = Vector3D.Normalize(horizontalDirection);
            }
            SetGyro(horizontalDirection);
        }

        private void SetGyro(Vector3D axis)
        {
            foreach (IMyGyro gyro in gyros)
            {
                gyro.Yaw = (float)axis.Dot(gyro.CubeGrid.WorldMatrix.Up);
                gyro.Pitch = (float)axis.Dot(gyro.CubeGrid.WorldMatrix.Right);
                gyro.Roll = (float)axis.Dot(gyro.CubeGrid.WorldMatrix.Backward);
            }
        }

        private void MakeGyroOver(bool over)
        {
            foreach (IMyGyro gyro in gyros)
            {
                gyro.Yaw = 0;
                gyro.Pitch = 0;
                gyro.Roll = 0;
                gyro.GyroOverride = over;
            }
            isGyroOver = over;
        }

        public void Main(string argument, UpdateType updateSource)
        {
            // The main entry point of the script, invoked every time
            // one of the programmable block's Run actions are invoked,
            // or the script updates itself. The updateSource argument
            // describes where the update came from. Be aware that the
            // updateSource is a  bitfield  and might contain more than 
            // one update type.
            // 
            // The method itself is required, but the arguments above
            // can be removed if not needed.
            switch (updateSource)
            {
                case UpdateType.None:
                    break;
                case UpdateType.Terminal:
                    DestinationWaypoint = vc.GPSToVector(argument);
                    Runtime.UpdateFrequency = UpdateFrequency.Once;
                    break;
                case UpdateType.Trigger:
                    break;
                case UpdateType.Mod:
                    break;
                case UpdateType.Script:
                    break;
                case UpdateType.Update1:
                    break;
                case UpdateType.Update10:
                    break;
                case UpdateType.Update100:
                    if (!isGyroOver)
                    {
                        //MakeGyroOver(true);
                    }
                    KeepDirectionTo(DestinationWaypoint);
                    if (ShipInProxyPoint(DestinationWaypoint))
                    {
                        //MakeGyroOver(false);
                        Runtime.UpdateFrequency = UpdateFrequency.None; //UpdateFrequency.Once;
                    }
                    break;
                case UpdateType.Once:
                    if (!checkFreeWay(DestinationWaypoint))
                    {
                        Runtime.UpdateFrequency = UpdateFrequency.Update100;
                    }
                    textPanel.WriteText("checkFreeWay true\n", true);
                    break;
                case UpdateType.IGC:
                    break;
                default:
                    break;
            }

        }
             

        //public void Save()
        //{
        //    // Called when the program needs to save its state. Use
        //    // this method to save your state to the Storage field
        //    // or some other means. 
        //    // 
        //    // This method is optional and can be removed if not
        //    // needed.
        //}

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
    }
}
