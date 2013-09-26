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
using System.Linq;
using System.Text;

using ARDrone.Control.Data;

namespace ARDrone.Control.Commands
{
    public enum DroneAnimation
    {
        PHI_M30_DEG = 0,
        PHI_30_DEG,
        THETA_M30_DEG,
        THETA_30_DEG,
        THETA_20DEG_YAW_200DEG,
        THETA_20DEG_YAW_M200DEG,
        TURNAROUND,
        TURNAROUND_GODOWN,
        YAW_SHAKE,
        YAW_DANCE,
        PHI_DANCE,
        THETA_DANCE,
        VZ_DANCE,
        WAVE,
        PHI_THETA_MIXED,
        DOUBLE_PHI_THETA_MIXED,
        FLIP_FRONT,
        FLIP_BACK,
        FLIP_LEFT,
        FLIP_RIGHT,
        MAYDAY
    }

    public class PlayDroneAnimationCommand : Command
    {
        private DroneAnimation droneAnimation;
        private int duration;

        public PlayDroneAnimationCommand(DroneAnimation droneAnimation, int duration)
        {
            this.droneAnimation = droneAnimation;
            this.duration = duration;
        }

        public override String CreateCommand(SupportedFirmwareVersion firmwareVersion)
        {
            CheckSequenceNumber();
            return String.Format("AT*CONFIG={0},\"control:flight_anim\",\"{1},{2}\"\r", sequenceNumber, (int)droneAnimation, duration);
            //return String.Format("AT*ANIM={0},{1},{2}\r", sequenceNumber, (int)droneAnimation, duration);
        }

    }
}
