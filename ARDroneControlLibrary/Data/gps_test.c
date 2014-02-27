#include <gps.h>

static struct gps_data_t gpsdata;
_declspec(dllexport) double get_time()
{
	return gpsdata.fix.time;
}
_declspec(dllexport) double get_lat()
{
	return gpsdata.fix.latitude;
}
_declspec(dllexport) double get_long()
{
	return  gpsdata.fix.longitude;
}

int main()
{
    char * hostName = "127.0.0.1";
    char * hostPort = "2947";

    // initializes a GPS-data structure to hold the data collected by
    // the GPS, and sets up access to gpsd
    if( -1 == gps_open(hostName, hostPort, &gpsdata) )
    {
        printf("ERROR: gps_open returned -1");
    }

    gps_stream(&gpsdata, WATCH_ENABLE | WATCH_JSON, NULL);

    /* Put this in a loop with a call to a high resolution sleep () in it. */
    while(1)
    {
        /* Used to check whether there is new data from the
         * daemon. The second argument is the maximum amount of time
         * to wait (in microseconds) on input before returning */
        if( gps_waiting(&gpsdata, 500) )
        {
            if( -1 == gps_read (&gpsdata) )
            {
                printf("ERROR: gps_read returned -1");
            }
            else
            {
                /* Display data from the GPS receiver. */
                printf("\nSatellites Used:%d\n", gpsdata.satellites_used);
                
                /* Error estimates are at 95% confidence. */

                printf("Time:%f, Error:%f\n", gpsdata.fix.time, gpsdata.fix.ept);
                printf("Latitude:%f, Error:%f\n", gpsdata.fix.latitude, gpsdata.fix.epy);
                printf("Longitude:%f, Error:%f\n", gpsdata.fix.longitude, gpsdata.fix.epx);
                printf("Altitude:%f, Error:%f\n", gpsdata.fix.altitude, gpsdata.fix.epv);
                printf("Track:%f, Error:%f\n", gpsdata.fix.track, gpsdata.fix.epd);
                printf("Speed:%f, Error:%f\n", gpsdata.fix.speed, gpsdata.fix.eps);
                printf("Climb:%f, Error:%f\n", gpsdata.fix.climb, gpsdata.fix.epc);
            }
        }
        
        usleep(250000);
    }

    /* When you are done... */
    (void) gps_stream(&gpsdata, WATCH_DISABLE, NULL);
    (void) gps_close (&gpsdata);
}
