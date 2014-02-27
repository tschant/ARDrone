/* ARDrone Control .NET - An application for flying the Parrot AR drone in Windows.
 * Copyright (C) 2010, 2011 Thomas Endres
 * 
 * This program is free software; you can redistribute it and/or modify it under the terms of the GNU General Public License as published by the Free Software Foundation; either version 3 of the License, or (at your option) any later version.
 * 
 * This program is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU General Public License for more details.
 * 
 * You should have received a copy of the GNU General Public License along with this program; if not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Threading;
using System.Diagnostics;

using ARDrone.Control.Data;
using ARDrone.Control.Events;
using ARDrone.Control.Network;
using ARDrone.Control.Utils;
using ARDrone.Control.Workers;


namespace ARDrone.Control.Workers
{
    
    public class GPSDataRetriever //This will retrieve and decode data written by putty log
    {
        //private DroneControl droneCtrl;

        private TelnetConnection drone;
        private string DRONE_CD = "cd /data/video/";
        private string GPSD_START = "gpsd -N -D -F /var/run/gpsd.sock /dev/ttyUSB0 &";
        private string GPS_START = "./gps_test";
        //Process gps_putty;
        

        public GPSDataRetriever(DroneControl droneCtrl)
        {
            drone = new TelnetConnection(droneCtrl.droneConfig.DroneIpAddress, 23);
            Connect();
        }

        private void Connect()
        {
            drone.WriteLine(DRONE_CD);
            drone.WriteLine(GPSD_START);
            drone.WriteLine(GPS_START);
            //Console.Write(drone.Read());
        }
        public string Write()
        {
            return drone.Read();
        }


    }
}
