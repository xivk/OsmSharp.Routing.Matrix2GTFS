using GeoAPI.Geometries;
using GTFS;
using GTFS.Entities;
using GTFS.IO;
using NetTopologySuite.Features;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;
using OsmSharp.Osm.PBF.Streams;
using OsmSharp.Routing.Osm.Interpreter;
using OsmSharp.Routing.TSP.Genetic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OsmSharp.Routing.Matrix2GTFS
{
    class Program
    {
        static void Main(string[] args)
        {
            OsmSharp.Logging.Log.Enable();
            OsmSharp.Logging.Log.RegisterListener(new OsmSharp.WinForms.UI.Logging.ConsoleTraceListener());
            // read the source file.
            var lines = OsmSharp.IO.DelimitedFiles.DelimitedFileHandler.ReadDelimitedFile(null,
                new FileInfo("bicylestations.csv").OpenRead(), OsmSharp.IO.DelimitedFiles.DelimiterType.DotCommaSeperated, true);

            // create router.
            var interpreter = new OsmRoutingInterpreter();
            var geoJsonWriter = new GeoJsonWriter();
            var router = Router.CreateLiveFrom(new PBFOsmStreamSource(new FileInfo("antwerp.osm.pbf").OpenRead()), interpreter);

            // resolve all points.
            var resolvedPoints = new List<RouterPoint>();
            var gtfs = new GTFS.GTFSFeed();
            var agency = new GTFS.Entities.Agency()
            {
                Id = "1",
                Name = "Velo Antwerpen",
                FareURL = "https://www.velo-antwerpen.be/"
            };
            gtfs.AddAgency(agency);
            foreach(var line in lines)
            {
                var latitude = double.Parse(line[0], System.Globalization.CultureInfo.InvariantCulture);
                var longitude = double.Parse(line[1], System.Globalization.CultureInfo.InvariantCulture);
                var refId = double.Parse(line[2], System.Globalization.CultureInfo.InvariantCulture);

                var resolved = router.Resolve(Vehicle.Bicycle, new Math.Geo.GeoCoordinate(latitude, longitude));
                if (resolved != null && router.CheckConnectivity(Vehicle.Bicycle, resolved, 100))
                { // point exists and is connected.
                    resolvedPoints.Add(resolved);
                    gtfs.AddStop(new Stop()
                        {
                            Id = (resolvedPoints.Count + 1).ToInvariantString(),
                            Name = refId.ToInvariantString(),
                            Code = refId.ToInvariantString(),
                            Latitude = latitude,
                            Longitude = longitude
                        });
                }
                else
                { // report that the point could not be resolved.
                    Console.WriteLine("Point with ref {0} could not be resolved!", refId);
                }
            }

            // calculate all routes.
            var matrix = router.CalculateManyToMany(Vehicle.Bicycle, resolvedPoints.ToArray(), resolvedPoints.ToArray());
            var routeId = 1;
            for (int x = 0; x < matrix.Length; x++)
            {
                for (int y = 0; y < matrix[x].Length; y++)
                {
                    var coordinates = matrix[x][y].GetPoints();
                    gtfs.AddRoute(new GTFS.Entities.Route()
                    {
                        Id = routeId.ToInvariantString(),
                        AgencyId = "1",
                        Description = "1"
                    });
                    gtfs.AddTrip(new GTFS.Entities.Trip()
                    {
                        Id = routeId.ToInvariantString(),
                        RouteId = routeId.ToInvariantString(),
                        ServiceId = routeId.ToInvariantString(),
                        ShapeId = routeId.ToInvariantString()
                    });
                    gtfs.AddStopTime(new GTFS.Entities.StopTime()
                    {
                        StopId = (x + 1).ToInvariantString(),
                        StopSequence = 0,
                        TripId = routeId.ToInvariantString(),
                        ArrivalTime = TimeOfDay.FromTotalSeconds(0),
                        DepartureTime = TimeOfDay.FromTotalSeconds(0)
                    });
                    gtfs.AddStopTime(new GTFS.Entities.StopTime()
                    {
                        StopId = (y + 1).ToInvariantString(),
                        StopSequence = 1,
                        TripId = routeId.ToInvariantString(),
                        ArrivalTime = TimeOfDay.FromTotalSeconds((int)matrix[x][y].TotalTime),
                        DepartureTime = TimeOfDay.FromTotalSeconds((int)matrix[x][y].TotalTime)
                    });
                    for (int idx = 0; idx < coordinates.Count; idx++)
                    {
                        gtfs.AddShape(new GTFS.Entities.Shape()
                        {
                            Id = routeId.ToInvariantString(),
                            Sequence = (uint)(idx + 1),
                            Latitude = coordinates[idx].Latitude,
                            Longitude = coordinates[idx].Longitude
                        });
                    }
                    routeId++;
                }
            }

            var feedWriter = new GTFSWriter<IGTFSFeed>();
            feedWriter.Write(gtfs, new GTFSDirectoryTarget(new DirectoryInfo(@"c:\temp\")));
        }
    }
}
