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

using ExcelLibrary.Office.Excel;



namespace ARDrone.Control.Workers
{
    
    public class GPSDataRetriever //This will retrieve and decode data written by putty log
    {
        //private DroneControl droneCtrl;

        private TelnetConnection drone;
        private string DRONE_CD = "cd /data/video/";
        private string SET_BAUD = "stty speed 57600 </dev/ttyUSB0 &";
        private string GPSD_START = "gpsd -D2 -F gpsd.sock /dev/ttyUSB0 &";
        private string GPS_START = "./gps_test &";

        private string GPS_REMOVE = "rm /data/video/gpsd.sock";
        private string GPS_KILL = "killall gpsd &";

        private string[] TEXT;
        private string NUMSATS;
        private string TIME;
        private string LAT;
        private string LONG;
        private string ALT;
        private string TRACK;
        private string SPEED;
        private string CLIMB;
        private int count;
        
        string file = "C:\\Users\\Tarryn\\Documents\\Visual Studio 2012\\ARDrone-Control-.NET-master\\GPSCoords.xls";
        

        public GPSDataRetriever(DroneControl droneCtrl)
        {
            count = 0;
            drone = new TelnetConnection(droneCtrl.droneConfig.DroneIpAddress, 23);
            Disconnect();
            System.Threading.Thread.Sleep(1000);
            Connect();
            
        }

        private void Connect()
        {
            Workbook workbook = new Workbook();
            Worksheet worksheet = new Worksheet("GPS");

            worksheet.Cells[count, 0] = new Cell("Latitude");
            worksheet.Cells[count, 1] = new Cell("Longitude");
            worksheet.Cells[count, 2] = new Cell("Name");
            count++;
            drone.WriteLine(DRONE_CD);
            drone.WriteLine(SET_BAUD);
            drone.WriteLine(GPSD_START);
            System.Threading.Thread.Sleep(1000);
            drone.WriteLine("");
            drone.WriteLine(GPS_START);

            workbook.Worksheets.Add(worksheet);
            workbook.Save(file);
        }
        private void Disconnect()
        {
            

            //TO-DO: WHY DOES THIS NOT WORK
            //GPS fails to restart after the initial run.... why?
            //only works after disconnecting battery
            drone.WriteLine(GPS_KILL);
            drone.WriteLine(GPS_REMOVE);
            //drone.WriteLine("lsof -i");
        }
        public string Read_Console()
        {
           return drone.Read();
        }

        public string parse()
        {
            Workbook book = Workbook.Open(file);
            Worksheet sheet = book.Worksheets[0];

            char[] delimiterChars = { ' ', ',', ':', '\t', '\n' };
            string text = Read_Console();
            TEXT = text.Split(delimiterChars);
            string[] words = text.Split(delimiterChars);

            for (int i = 0; i < TEXT.Length - 1; i++)
            {
                if (TEXT[i] == "Used" || TEXT[i] == "Satellites Used")
                    NUMSATS = TEXT[i + 1];
                else if (TEXT[i] == "Time")
                    TIME = TEXT[i + 1];
                else if (TEXT[i] == "Latitude")
                    LAT = TEXT[i + 1];
                else if (TEXT[i] == "Longitude")
                    LONG = TEXT[i + 1];
                else if (TEXT[i] == "Altitude")
                    ALT = TEXT[i + 1];
                else if (TEXT[i] == "Track")
                    TRACK = TEXT[i + 1];
                else if (TEXT[i] == "Speed")
                    SPEED = TEXT[i + 1];
                else if (TEXT[i] == "Climb")
                    CLIMB = TEXT[i + 1];
            }
            sheet.Cells[count, 2] = new Cell(count);
            sheet.Cells[count, 0] = new Cell(LAT);
            sheet.Cells[count, 1] = new Cell(LONG);

            //worksheet.Cells[count, 0] = new Cell(LAT);
            count++;
            book.Worksheets.Clear();
            book.Worksheets.Add(sheet);
            book.Save(file);

            return text;
        }
        public string[] getTEXT()
        {
            return TEXT;
        }
        public string getNumSats()
        {
            return NUMSATS;
        }
        public string getTime()
        {
            return TIME;
        }

        public string getLongitude()
        {
            return LONG;
        }

        public string getLatitude()
        {
            return LAT;
        }

        public string getAltitude()
        {
            return ALT;
        }

        public string getTrack()
        {
            return TRACK;
        }

        public string getSpeed()
        {
            return SPEED;
        }

        public string getClimb()
        {
            return CLIMB;
        }

    }
}
