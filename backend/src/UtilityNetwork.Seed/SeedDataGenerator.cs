using System.Globalization;

namespace UtilityNetwork.Seed;

public static class SeedDataGenerator
{
    public static readonly Guid WaterNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    public static readonly Guid UrbanNetworkId = Guid.Parse("a0000000-0000-0000-0000-000000000002");

    private static readonly string[] Statuses = ["ACTIVE", "ACTIVE", "ACTIVE", "ACTIVE", "PLANNED", "INACTIVE", "DAMAGED", "ABANDONED"];
    private static readonly string[] Creators = ["admin", "editor"];

    public static SeedDataset Generate(string size)
    {
        // Fixed random seed for 100% reproducible test data
        var rng = new Random(42);

        var points = new List<SeedPointRecord>();
        var lines = new List<SeedLineRecord>();
        var polygons = new List<SeedPolygonRecord>();

        // 1. Planted Errors Catalog (guaranteed in both small and medium)
        var plantedErrors = PlantErrors(points, lines, polygons);

        // 2. Base Water Network
        GenerateWaterNetwork(rng, points, lines, polygons, size);

        // 3. Urban Assets Network
        GenerateUrbanAssets(rng, points, lines, polygons, size);

        return new SeedDataset(points, lines, polygons, plantedErrors);
    }

    private static List<string> PlantErrors(
        List<SeedPointRecord> points,
        List<SeedLineRecord> lines,
        List<SeedPolygonRecord> polygons)
    {
        var errorDescriptions = new List<string>
        {
            "1. [URBAN/BUILDING] Near-duplicate buildings: BLD-ERR-01A & BLD-ERR-01B (~99% footprint overlap)",
            "2. [URBAN/SMART_POLE] Overlapping poles: POLE-ERR-02A & POLE-ERR-02B (identical coordinate 31.7512, 30.0165)",
            "3. [URBAN/ROAD] Very short road segment: ROAD-ERR-03 (length ~0.4m < 1m threshold)",
            "4. [URBAN/BUILDING] Building overlapping road: BLD-ERR-04 overlapping ROAD-ERR-04",
            "5. [WATER/VALVE] Valve off-pipe: VALVE-ERR-05 (5m clearance from nearest pipe)",
            "6. [WATER/PIPE] Dangling pipe end: PIPE-ERR-06 (no junction/terminal at end)",
            "7. [WATER/HYDRANT] Duplicate hydrants: HYD-ERR-07A & HYD-ERR-07B (identical coordinate 31.7540, 30.0180)",
            "8. [WATER/PIPE] Unknown flow direction: PIPE-ERR-08 (flow_direction = 'UNKNOWN')",
            "9. [WATER/PIPE] Reversed/Unfed direction: PIPE-ERR-09 (opposing digitized flow direction)",
            "10. [WATER/PIPE] Segment with no source: PIPE-ERR-10 (isolated network without reservoir/pump)"
        };

        var errorBaseTime = DateTime.UtcNow.AddDays(-15);

        // 1. Near-duplicate buildings
        polygons.Add(new SeedPolygonRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000001a"),
            UrbanNetworkId,
            "BUILDING",
            "مبنى تجاري مكرر أ / Commercial Building Duplicate A",
            "ACTIVE",
            """{"building_type": "COMMERCIAL", "floors_count": 8, "construction_year": 2022, "height_m": 32.0}""",
            [[
                [31.74500, 30.01200],
                [31.74550, 30.01200],
                [31.74550, 30.01250],
                [31.74500, 30.01250],
                [31.74500, 30.01200]
            ]],
            "editor",
            errorBaseTime,
            "BLD-ERR-01A",
            "SEED_GENERATOR"
        ));

        polygons.Add(new SeedPolygonRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000001b"),
            UrbanNetworkId,
            "BUILDING",
            "مبنى تجاري مكرر ب / Commercial Building Duplicate B",
            "ACTIVE",
            """{"building_type": "COMMERCIAL", "floors_count": 8, "construction_year": 2022, "height_m": 32.0}""",
            [[
                [31.74501, 30.01201],
                [31.74551, 30.01201],
                [31.74551, 30.01251],
                [31.74501, 30.01251],
                [31.74501, 30.01201]
            ]],
            "editor",
            errorBaseTime.AddHours(1),
            "BLD-ERR-01B",
            "SEED_GENERATOR"
        ));

        // 2. Overlapping poles
        points.Add(new SeedPointRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000002a"),
            UrbanNetworkId,
            "SMART_POLE",
            "عمود إنارة ذكي مكرر أ / Smart Pole Overlap A",
            "ACTIVE",
            null,
            """{"pole_type": "INTEGRATED_SMART_POLE", "height_m": 12.0, "has_cctv": true, "has_wifi": true}""",
            31.75120,
            30.01650,
            "admin",
            errorBaseTime,
            "POLE-ERR-02A",
            "SEED_GENERATOR"
        ));

        points.Add(new SeedPointRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000002b"),
            UrbanNetworkId,
            "SMART_POLE",
            "عمود إنارة ذكي مكرر ب / Smart Pole Overlap B",
            "ACTIVE",
            null,
            """{"pole_type": "STREET_LIGHT", "height_m": 10.0, "has_cctv": false, "has_wifi": false}""",
            31.75120,
            30.01650,
            "editor",
            errorBaseTime.AddHours(2),
            "POLE-ERR-02B",
            "SEED_GENERATOR"
        ));

        // 3. Very short road segment (0.4m)
        lines.Add(new SeedLineRecord(
            Guid.Parse("b0000000-0000-0000-0000-000000000003"),
            UrbanNetworkId,
            "ROAD",
            "قطاع طريق متناهي الصغر / Ultra-Short Road Segment",
            "ACTIVE",
            "WITH_DIGITIZED",
            null,
            "ASPHALT",
            """{"road_class": "LOCAL", "lanes_count": 2, "speed_limit_kmh": 40, "width_m": 8.0}""",
            [
                [31.748000, 30.018000],
                [31.748003, 30.018003]
            ],
            "editor",
            errorBaseTime,
            "ROAD-ERR-03",
            "SEED_GENERATOR"
        ));

        // 4. Building polygon overlapping a road
        lines.Add(new SeedLineRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000004a"),
            UrbanNetworkId,
            "ROAD",
            "طريق الخدمة المتداخل / Service Road Under Building",
            "ACTIVE",
            "WITH_DIGITIZED",
            null,
            "ASPHALT",
            """{"road_class": "LOCAL", "lanes_count": 2, "speed_limit_kmh": 50, "width_m": 10.0}""",
            [
                [31.74100, 30.01425],
                [31.74300, 30.01425]
            ],
            "admin",
            errorBaseTime,
            "ROAD-ERR-04",
            "SEED_GENERATOR"
        ));

        polygons.Add(new SeedPolygonRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000004b"),
            UrbanNetworkId,
            "BUILDING",
            "مبنى متداخل مع الطريق / Building Overlapping Road",
            "ACTIVE",
            """{"building_type": "ADMINISTRATIVE", "floors_count": 5, "construction_year": 2023, "height_m": 22.0}""",
            [[
                [31.74180, 30.01400],
                [31.74240, 30.01400],
                [31.74240, 30.01460],
                [31.74180, 30.01460],
                [31.74180, 30.01400]
            ]],
            "editor",
            errorBaseTime,
            "BLD-ERR-04",
            "SEED_GENERATOR"
        ));

        // 5. Valve off-pipe (isolated valve with 5m gap from any line)
        points.Add(new SeedPointRecord(
            Guid.Parse("b0000000-0000-0000-0000-000000000005"),
            WaterNetworkId,
            "VALVE",
            "محبس خارج خط الأنابيب / Floating Valve Off-Pipe",
            "ACTIVE",
            true,
            """{"valve_type": "GATE", "depth_m": 1.5, "turns_to_close": 12}""",
            31.75800,
            30.02300,
            "editor",
            errorBaseTime,
            "VALVE-ERR-05",
            "SEED_GENERATOR"
        ));

        // 6. Dangling pipe end
        lines.Add(new SeedLineRecord(
            Guid.Parse("b0000000-0000-0000-0000-000000000006"),
            WaterNetworkId,
            "PIPE",
            "أنبوب بنهاية معلقة دون مخرج / Dangling Pipe End",
            "ACTIVE",
            "WITH_DIGITIZED",
            200,
            "DUCTILE_IRON",
            """{"installation_year": 2023, "pressure_bar": 6.0}""",
            [
                [31.76000, 30.01500],
                [31.76150, 30.01450]
            ],
            "editor",
            errorBaseTime,
            "PIPE-ERR-06",
            "SEED_GENERATOR"
        ));

        // 7. Duplicate hydrants
        points.Add(new SeedPointRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000007a"),
            WaterNetworkId,
            "HYDRANT",
            "حنفية حريق مكررة أ / Fire Hydrant Duplicate A",
            "ACTIVE",
            null,
            """{"flow_rate_lps": 25.0, "outlet_diameter_mm": 65}""",
            31.75400,
            30.01800,
            "admin",
            errorBaseTime,
            "HYD-ERR-07A",
            "SEED_GENERATOR"
        ));

        points.Add(new SeedPointRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000007b"),
            WaterNetworkId,
            "HYDRANT",
            "حنفية حريق مكررة ب / Fire Hydrant Duplicate B",
            "ACTIVE",
            null,
            """{"flow_rate_lps": 25.0, "outlet_diameter_mm": 65}""",
            31.75400,
            30.01800,
            "editor",
            errorBaseTime.AddHours(3),
            "HYD-ERR-07B",
            "SEED_GENERATOR"
        ));

        // 8. Pipe with UNKNOWN flow direction
        lines.Add(new SeedLineRecord(
            Guid.Parse("b0000000-0000-0000-0000-000000000008"),
            WaterNetworkId,
            "PIPE",
            "خط مياه باتجاه سريان مجهول / Water Pipe Unknown Direction",
            "ACTIVE",
            "UNKNOWN",
            300,
            "HDPE",
            """{"installation_year": 2022, "pressure_bar": 5.5}""",
            [
                [31.75400, 30.01950],
                [31.75600, 30.01950]
            ],
            "editor",
            errorBaseTime,
            "PIPE-ERR-08",
            "SEED_GENERATOR"
        ));

        // 9. Pipe with reversed / opposing flow direction
        lines.Add(new SeedLineRecord(
            Guid.Parse("b0000000-0000-0000-0000-000000000009"),
            WaterNetworkId,
            "PIPE",
            "خط مياه معكوس الاتجاه / Water Pipe Reversed Flow Direction",
            "ACTIVE",
            "AGAINST_DIGITIZED",
            250,
            "DUCTILE_IRON",
            """{"installation_year": 2021, "pressure_bar": 5.0}""",
            [
                [31.75600, 30.01950],
                [31.75800, 30.01950]
            ],
            "editor",
            errorBaseTime,
            "PIPE-ERR-09",
            "SEED_GENERATOR"
        ));

        // 10. Segment with no source (isolated island pipe network)
        lines.Add(new SeedLineRecord(
            Guid.Parse("b0000000-0000-0000-0000-000000000010"),
            WaterNetworkId,
            "PIPE",
            "شبكة مياه معزولة بلا مصدر تغذية / Isolated Unfed Pipe Network",
            "ACTIVE",
            "WITH_DIGITIZED",
            150,
            "UPVC",
            """{"installation_year": 2020, "pressure_bar": 4.0}""",
            [
                [31.71000, 30.04500],
                [31.71200, 30.04500]
            ],
            "admin",
            errorBaseTime,
            "PIPE-ERR-10",
            "SEED_GENERATOR"
        ));

        points.Add(new SeedPointRecord(
            Guid.Parse("b0000000-0000-0000-0000-00000000010a"),
            WaterNetworkId,
            "HYDRANT",
            "حنفية حريق على شبكة معزولة / Fire Hydrant on Isolated Pipe",
            "ACTIVE",
            null,
            """{"flow_rate_lps": 20.0, "outlet_diameter_mm": 65}""",
            31.71200,
            30.04500,
            "editor",
            errorBaseTime,
            "HYD-ERR-10",
            "SEED_GENERATOR"
        ));

        return errorDescriptions;
    }

    private static void GenerateWaterNetwork(
        Random rng,
        List<SeedPointRecord> points,
        List<SeedLineRecord> lines,
        List<SeedPolygonRecord> polygons,
        string size)
    {
        var isMedium = size is "medium" or "stress";
        var clustersCount = size switch
        {
            "stress" => 16,
            "medium" => 4,
            _ => 1
        };

        for (var c = 0; c < clustersCount; c++)
        {
            var clusterOffsetLon = c * 0.025;
            var clusterOffsetLat = (c % 2) * 0.020;

            // Reservoir (Source)
            var resId = Guid.NewGuid();
            var resX = 31.75000 + clusterOffsetLon;
            var resY = 30.02000 + clusterOffsetLat;
            var resTime = DateTime.UtcNow.AddDays(-rng.Next(1, 28));

            points.Add(new SeedPointRecord(
                resId,
                WaterNetworkId,
                "RESERVOIR",
                $"خزان مياه R{c + 1} - القطاع الرئيسي / Main Reservoir R{c + 1} - Central Sector",
                "ACTIVE",
                null,
                string.Create(CultureInfo.InvariantCulture, $$"""{"capacity_m3": {{40000 + c * 10000}}, "water_level_m": {{12.5 + c * 1.5}}}"""),
                resX,
                resY,
                "admin",
                resTime,
                $"RES-R{c + 1}",
                "SEED_GENERATOR"
            ));

            // Pump Station (Source)
            var psId = Guid.NewGuid();
            var psX = resX + 0.0010;
            var psY = resY - 0.0005;
            points.Add(new SeedPointRecord(
                psId,
                WaterNetworkId,
                "PUMP_STATION",
                $"محطة ضخ مياه الشرب PS{c + 1} / Booster Pump Station PS{c + 1}",
                "ACTIVE",
                null,
                string.Create(CultureInfo.InvariantCulture, $$"""{"power_kw": {{350 + c * 100}}, "pumps_count": {{3 + c}}}"""),
                psX,
                psY,
                "admin",
                resTime.AddHours(2),
                $"PS-{c + 1}",
                "SEED_GENERATOR"
            ));

            // Transmission main from Reservoir to Pump Station
            lines.Add(new SeedLineRecord(
                Guid.NewGuid(),
                WaterNetworkId,
                "PIPE",
                $"أنبوب التغذية الرئيسي R{c + 1}-PS{c + 1} / Suction Transmission Main",
                "ACTIVE",
                "WITH_DIGITIZED",
                600,
                "DUCTILE_IRON",
                """{"installation_year": 2021, "pressure_bar": 10.0}""",
                [[resX, resY], [psX, psY]],
                "admin",
                resTime.AddHours(3),
                $"PIPE-TR-{c + 1}",
                "SEED_GENERATOR"
            ));

            // Grid distribution: generate a grid of pipes, valves, fittings, and hydrants
            var gridRows = size == "stress" ? 12 : (size == "medium" ? 6 : 4);
            var gridCols = size == "stress" ? 16 : (size == "medium" ? 8 : 5);

            var prevNode = new double[] { psX, psY };

            for (var r = 0; r < gridRows; r++)
            {
                var rowY = resY - 0.0020 - (r * 0.0025);
                var rowStartNode = new double[] { resX, rowY };

                // Feed pipe from previous node down to this row
                lines.Add(new SeedLineRecord(
                    Guid.NewGuid(),
                    WaterNetworkId,
                    "PIPE",
                    $"خط مياه رئيسي قطاع {c + 1}-{r + 1} / Distribution Trunk Main {c + 1}-{r + 1}",
                    Statuses[rng.Next(Statuses.Length)],
                    "WITH_DIGITIZED",
                    400,
                    "DUCTILE_IRON",
                    string.Create(CultureInfo.InvariantCulture, $$"""{"installation_year": {{2020 + (r % 4)}}, "pressure_bar": {{6.5 - (r * 0.4)}}}"""),
                    [prevNode, rowStartNode],
                    Creators[rng.Next(Creators.Length)],
                    DateTime.UtcNow.AddDays(-rng.Next(1, 25)),
                    $"PIPE-TRK-{c}-{r}",
                    "SEED_GENERATOR"
                ));

                // Fitting at junction
                points.Add(new SeedPointRecord(
                    Guid.NewGuid(),
                    WaterNetworkId,
                    "FITTING",
                    $"تفريعة أنابيب TEE-{c + 1}-{r + 1} / Pipe Tee Fitting",
                    "ACTIVE",
                    null,
                    """{"fitting_type": "TEE"}""",
                    rowStartNode[0],
                    rowStartNode[1],
                    "editor",
                    DateTime.UtcNow.AddDays(-rng.Next(1, 20)),
                    $"FIT-T-{c}-{r}",
                    "SEED_GENERATOR"
                ));

                var currentCoord = rowStartNode;

                for (var col = 0; col < gridCols; col++)
                {
                    var nextCoord = new double[] { currentCoord[0] + 0.0020, rowY };
                    var pipeId = Guid.NewGuid();

                    // Pipe segment
                    lines.Add(new SeedLineRecord(
                        pipeId,
                        WaterNetworkId,
                        "PIPE",
                        $"أنبوب توزيع مياه D-{c + 1}-{r + 1}-{col + 1} / Water Distribution Pipe",
                        Statuses[rng.Next(Statuses.Length)],
                        "WITH_DIGITIZED",
                        200,
                        "HDPE",
                        string.Create(CultureInfo.InvariantCulture, $$"""{"installation_year": {{2021 + (col % 3)}}, "pressure_bar": {{5.0 - (col * 0.2)}}}"""),
                        [currentCoord, nextCoord],
                        Creators[rng.Next(Creators.Length)],
                        DateTime.UtcNow.AddDays(-rng.Next(1, 20)),
                        $"PIPE-DIST-{c}-{r}-{col}",
                        "SEED_GENERATOR"
                    ));

                    // Place Valve along the line or at node
                    if (col % 2 == 0)
                    {
                        var valveMidX = (currentCoord[0] + nextCoord[0]) / 2.0;
                        var valveTypes = new[] { "GATE", "BUTTERFLY", "CHECK", "PRESSURE_REDUCING" };
                        points.Add(new SeedPointRecord(
                            Guid.NewGuid(),
                            WaterNetworkId,
                            "VALVE",
                            $"محبس عزل خط المياه V-{c + 1}-{r + 1}-{col + 1} / Isolation Valve",
                            Statuses[rng.Next(Statuses.Length)],
                            rng.Next(10) > 1, // 90% open, 10% closed
                            string.Create(CultureInfo.InvariantCulture, $$"""{"valve_type": "{{valveTypes[rng.Next(valveTypes.Length)]}}", "depth_m": {{1.2 + (col * 0.1)}}, "turns_to_close": {{10 + col * 2}}}"""),
                            valveMidX,
                            rowY,
                            Creators[rng.Next(Creators.Length)],
                            DateTime.UtcNow.AddDays(-rng.Next(1, 25)),
                            $"VALVE-{c}-{r}-{col}",
                            "SEED_GENERATOR"
                        ));
                    }

                    // Terminal Hydrant or Junction Fitting at node end
                    if (col == gridCols - 1)
                    {
                        points.Add(new SeedPointRecord(
                            Guid.NewGuid(),
                            WaterNetworkId,
                            "HYDRANT",
                            $"حنفية حريق طرفية H-{c + 1}-{r + 1} / Terminal Fire Hydrant",
                            Statuses[rng.Next(Statuses.Length)],
                            null,
                            string.Create(CultureInfo.InvariantCulture, $$"""{"flow_rate_lps": {{30.0 + (r * 2.5)}}, "outlet_diameter_mm": 65}"""),
                            nextCoord[0],
                            nextCoord[1],
                            "editor",
                            DateTime.UtcNow.AddDays(-rng.Next(1, 20)),
                            $"HYD-{c}-{r}",
                            "SEED_GENERATOR"
                        ));
                    }
                    else
                    {
                        points.Add(new SeedPointRecord(
                            Guid.NewGuid(),
                            WaterNetworkId,
                            "FITTING",
                            $"وصلة تقاطع FIT-{c + 1}-{r + 1}-{col + 1} / Pipe Cross Fitting",
                            "ACTIVE",
                            null,
                            """{"fitting_type": "CROSS"}""",
                            nextCoord[0],
                            nextCoord[1],
                            "editor",
                            DateTime.UtcNow.AddDays(-rng.Next(1, 20)),
                            $"FIT-C-{c}-{r}-{col}",
                            "SEED_GENERATOR"
                        ));
                    }

                    currentCoord = nextCoord;
                }

                prevNode = rowStartNode;
            }

            // Service Zones (Polygons)
            var zonesInCluster = isMedium ? 4 : 5;
            for (var z = 0; z < zonesInCluster; z++)
            {
                var zMinX = resX + (z * 0.0035);
                var zMaxX = zMinX + 0.0030;
                var zMinY = resY - 0.0150;
                var zMaxY = resY - 0.0010;

                polygons.Add(new SeedPolygonRecord(
                    Guid.NewGuid(),
                    WaterNetworkId,
                    "SERVICE_ZONE",
                    $"منطقة خدمة ضغط مياه SZ-{c + 1}-{z + 1} / Pressure Service Zone {c + 1}-{z + 1}",
                    "ACTIVE",
                    string.Create(CultureInfo.InvariantCulture, $$"""{"target_pressure_bar": {{4.0 + (z * 0.5)}}, "population_served": {{15000 + (z * 5000)}}}"""),
                    [[
                        [zMinX, zMinY],
                        [zMaxX, zMinY],
                        [zMaxX, zMaxY],
                        [zMinX, zMaxY],
                        [zMinX, zMinY]
                    ]],
                    "admin",
                    DateTime.UtcNow.AddDays(-rng.Next(5, 30)),
                    $"SZ-{c + 1}-{z + 1}",
                    "SEED_GENERATOR"
                ));
            }
        }
    }

    private static void GenerateUrbanAssets(
        Random rng,
        List<SeedPointRecord> points,
        List<SeedLineRecord> lines,
        List<SeedPolygonRecord> polygons,
        string size)
    {
        var isMedium = size is "medium" or "stress";
        var clustersCount = size switch
        {
            "stress" => 16,
            "medium" => 4,
            _ => 1
        };
        var buildingTypes = new[] { "RESIDENTIAL", "COMMERCIAL", "ADMINISTRATIVE", "GOVERNMENTAL", "EDUCATIONAL", "HEALTHCARE" };
        var roadClasses = new[] { "HIGHWAY", "ARTERIAL", "COLLECTOR", "LOCAL", "PEDESTRIAN" };
        var poleTypes = new[] { "STREET_LIGHT", "CCTV", "WIFI_HOTSPOT", "ENVIRONMENTAL_SENSOR", "INTEGRATED_SMART_POLE" };
        var treeSpecies = new[] { "Phoenix dactylifera (نخيل بلحي)", "Ficus nitida (فيكس نيتدا)", "Acacia tortilis (سنط)", "Jacaranda mimosifolia (جاكاراندا)" };
        var landUses = new[] { "MIXED_USE", "RESIDENTIAL", "COMMERCIAL", "PUBLIC_UTILITY", "GREEN_SPACE" };

        for (var c = 0; c < clustersCount; c++)
        {
            var clusterBaseLon = 31.73000 + (c * 0.025);
            var clusterBaseLat = 30.00500 + ((c % 2) * 0.020);

            // 1. Roads
            var roadsInCluster = isMedium ? 25 : 35;
            for (var r = 0; r < roadsInCluster; r++)
            {
                var rY = clusterBaseLat + (r * 0.0008);
                var roadClass = roadClasses[rng.Next(roadClasses.Length)];
                var lanes = roadClass switch
                {
                    "HIGHWAY" => 8,
                    "ARTERIAL" => 6,
                    "COLLECTOR" => 4,
                    _ => 2
                };
                var speed = roadClass switch
                {
                    "HIGHWAY" => 100,
                    "ARTERIAL" => 80,
                    "COLLECTOR" => 60,
                    _ => 40
                };
                var width = lanes * 3.5;

                var rStartX = clusterBaseLon;
                var rEndX = clusterBaseLon + 0.0180;

                lines.Add(new SeedLineRecord(
                    Guid.NewGuid(),
                    UrbanNetworkId,
                    "ROAD",
                    $"طريق قطاع C{c + 1}-R{r + 1} / Urban Road Corridor C{c + 1}-R{r + 1}",
                    Statuses[rng.Next(Statuses.Length)],
                    "WITH_DIGITIZED",
                    null,
                    "ASPHALT",
                    string.Create(CultureInfo.InvariantCulture, $$"""{"road_class": "{{roadClass}}", "lanes_count": {{lanes}}, "speed_limit_kmh": {{speed}}, "width_m": {{width}}}"""),
                    [[rStartX, rY], [rEndX, rY]],
                    Creators[rng.Next(Creators.Length)],
                    DateTime.UtcNow.AddDays(-rng.Next(1, 25)),
                    $"ROAD-C{c}-R{r}",
                    "SEED_GENERATOR"
                ));

                // Smart Poles along road
                var polesOnRoad = isMedium ? 3 : 2;
                for (var p = 0; p < polesOnRoad; p++)
                {
                    var pX = rStartX + ((p + 1) * (0.0180 / (polesOnRoad + 1)));
                    var pType = poleTypes[rng.Next(poleTypes.Length)];
                    points.Add(new SeedPointRecord(
                        Guid.NewGuid(),
                        UrbanNetworkId,
                        "SMART_POLE",
                        $"عمود إنارة ذكي SP-C{c + 1}-{r + 1}-{p + 1} / Smart Pole",
                        Statuses[rng.Next(Statuses.Length)],
                        null,
                        string.Create(CultureInfo.InvariantCulture, $$"""{"pole_type": "{{pType}}", "height_m": {{8.0 + (p * 2.0)}}, "has_cctv": {{(p % 2 == 0 ? "true" : "false")}}, "has_wifi": {{(p % 3 == 0 ? "true" : "false")}}}"""),
                        pX,
                        rY + 0.0001,
                        Creators[rng.Next(Creators.Length)],
                        DateTime.UtcNow.AddDays(-rng.Next(1, 20)),
                        $"POLE-C{c}-R{r}-{p}",
                        "SEED_GENERATOR"
                    ));
                }

                // Trees along road
                var treesOnRoad = isMedium ? 4 : 3;
                for (var t = 0; t < treesOnRoad; t++)
                {
                    var tX = rStartX + ((t + 0.5) * (0.0180 / treesOnRoad));
                    points.Add(new SeedPointRecord(
                        Guid.NewGuid(),
                        UrbanNetworkId,
                        "TREE",
                        $"شجرة مسطحات خضراء TR-C{c + 1}-{r + 1}-{t + 1} / Landscaping Tree",
                        "ACTIVE",
                        null,
                        string.Create(CultureInfo.InvariantCulture, $$"""{"species": "{{treeSpecies[rng.Next(treeSpecies.Length)]}}", "height_m": {{4.0 + (t * 1.5)}}, "crown_diameter_m": {{3.0 + (t * 0.5)}}}"""),
                        tX,
                        rY - 0.0001,
                        "editor",
                        DateTime.UtcNow.AddDays(-rng.Next(1, 20)),
                        $"TREE-C{c}-R{r}-{t}",
                        "SEED_GENERATOR"
                    ));
                }
            }

            // 2. Buildings (Polygons)
            var buildingsInCluster = isMedium ? 130 : 60;
            for (var b = 0; b < buildingsInCluster; b++)
            {
                var bType = buildingTypes[rng.Next(buildingTypes.Length)];
                var floors = bType switch
                {
                    "COMMERCIAL" => rng.Next(15, 60),
                    "ADMINISTRATIVE" => rng.Next(6, 20),
                    "GOVERNMENTAL" => rng.Next(4, 12),
                    _ => rng.Next(3, 10)
                };
                var height = floors * 3.8;
                var year = rng.Next(2020, 2024);

                var bBaseX = clusterBaseLon + 0.0010 + ((b % 10) * 0.0016);
                var bBaseY = clusterBaseLat + 0.0020 + ((b / 10) * 0.0015);
                var bWidth = 0.0006;
                var bHeight = 0.0005;

                polygons.Add(new SeedPolygonRecord(
                    Guid.NewGuid(),
                    UrbanNetworkId,
                    "BUILDING",
                    $"مبنى {bType} C{c + 1}-B{b + 1} / Urban Building C{c + 1}-B{b + 1}",
                    Statuses[rng.Next(Statuses.Length)],
                    string.Create(CultureInfo.InvariantCulture, $$"""{"building_type": "{{bType}}", "floors_count": {{floors}}, "construction_year": {{year}}, "height_m": {{height}}}"""),
                    [[
                        [bBaseX, bBaseY],
                        [bBaseX + bWidth, bBaseY],
                        [bBaseX + bWidth, bBaseY + bHeight],
                        [bBaseX, bBaseY + bHeight],
                        [bBaseX, bBaseY]
                    ]],
                    Creators[rng.Next(Creators.Length)],
                    DateTime.UtcNow.AddDays(-rng.Next(1, 28)),
                    $"BLD-C{c}-B{b}",
                    "SEED_GENERATOR"
                ));
            }

            // 3. Parcels (Polygons)
            var parcelsInCluster = isMedium ? 30 : 15;
            for (var p = 0; p < parcelsInCluster; p++)
            {
                var pUse = landUses[rng.Next(landUses.Length)];
                var pBaseX = clusterBaseLon + ((p % 5) * 0.0035);
                var pBaseY = clusterBaseLat + ((p / 5) * 0.0050);
                var pWidth = 0.0030;
                var pHeight = 0.0040;

                polygons.Add(new SeedPolygonRecord(
                    Guid.NewGuid(),
                    UrbanNetworkId,
                    "PARCEL",
                    $"قطعة أرض استثمارية P-C{c + 1}-{p + 1} / Land Parcel P-C{c + 1}-{p + 1}",
                    "ACTIVE",
                    string.Create(CultureInfo.InvariantCulture, $$"""{"land_use": "{{pUse}}", "zoning_code": "ACUD-Z{{c + 1}}-{{p + 1}}", "area_sqm": {{rng.Next(5000, 25000)}}}"""),
                    [[
                        [pBaseX, pBaseY],
                        [pBaseX + pWidth, pBaseY],
                        [pBaseX + pWidth, pBaseY + pHeight],
                        [pBaseX, pBaseY + pHeight],
                        [pBaseX, pBaseY]
                    ]],
                    "admin",
                    DateTime.UtcNow.AddDays(-rng.Next(10, 30)),
                    $"PRCL-C{c}-P{p}",
                    "SEED_GENERATOR"
                ));
            }
        }
    }
}
