using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
using Autodesk.Revit.DB.Structure;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Selection;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RevitAPI_lesson430
{

    [TransactionAttribute(TransactionMode.Manual)]

    public class CreationModel : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uIDoc = commandData.Application.ActiveUIDocument;
            Document doc = uIDoc.Document;

            List<Level> levels = FindAllLevels(doc);
            Level level1 = FindLevel(levels, "Уровень 1");
            Level level2 = FindLevel(levels, "Уровень 2");

            List<XYZ> points = GetPointsForWall(10000, 5000);

            List<Wall> walls = new List<Wall>();

            using (Transaction transaction = new Transaction(doc, "Creation"))
            {
                transaction.Start();

                walls = CreateWalls(doc, level1, level2, points);

                CreateDoor(doc, level1, walls[0]);

                for (int i = 1; i < walls.Count; i++)
                {
                    CreateWindow(doc, level1, walls[i]);
                }

                //CreateRoof(doc, level2, walls);
                CreateExtrusionRoof (doc, level2, walls);

                transaction.Commit();
            }

            return Result.Succeeded;
        }

        private void CreateExtrusionRoof(Document doc, Level levelRoof, List<Wall> baseWalls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм - С заливкой"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidht = baseWalls[0].Width;
            double dt = wallWidht / 2;

            XYZ pointBound1 = (baseWalls[1].Location as LocationCurve).Curve.GetEndPoint(0);
            pointBound1 += new XYZ(0, -dt, 0);
            XYZ pointBound3 = (baseWalls[1].Location as LocationCurve).Curve.GetEndPoint(1);
            pointBound3 += new XYZ(0, +dt, 0);
            XYZ pointBound2 = new XYZ(
                                 (pointBound1.X + pointBound3.X) / 2,
                                 (pointBound1.Y + pointBound3.Y) / 2,
                                 UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters));

            CurveArray curveArray = new CurveArray();
            curveArray.Append(Line.CreateBound(pointBound1, pointBound2));
            curveArray.Append(Line.CreateBound(pointBound2, pointBound3));

            ReferencePlane plane = doc.Create.NewReferencePlane2(pointBound1, pointBound2, pointBound3, doc.ActiveView);

            double lengthRoof = (baseWalls[0].Location as LocationCurve).Curve.Length + dt;

            var roof = doc.Create.NewExtrusionRoof(curveArray, plane, levelRoof, roofType, -dt, lengthRoof);

            XYZ movePoint = new XYZ (0,0, levelRoof.Elevation);
            ElementTransformUtils.MoveElement(doc, roof.Id, movePoint);
        }

        private void CreateRoof(Document doc, Level levelRoof, List<Wall> baseWalls)
        {
            RoofType roofType = new FilteredElementCollector(doc)
                .OfClass(typeof(RoofType))
                .OfType<RoofType>()
                .Where(x => x.Name.Equals("Типовой - 400мм - С заливкой"))
                .Where(x => x.FamilyName.Equals("Базовая крыша"))
                .FirstOrDefault();

            double wallWidht = baseWalls[0].Width;
            double dt = wallWidht / 2;

            List<XYZ> pointsOffset = new List<XYZ>();
            pointsOffset.Add(new XYZ(-dt, -dt, 0));
            pointsOffset.Add(new XYZ(dt, -dt, 0));
            pointsOffset.Add(new XYZ(dt, dt, 0));
            pointsOffset.Add(new XYZ(-dt, dt, 0));
            pointsOffset.Add(new XYZ(-dt, -dt, 0));

            Application application = doc.Application;
            CurveArray footPrint = application.Create.NewCurveArray();
            for (int i = 0; i < baseWalls.Count; i++)
            {
                LocationCurve curve = baseWalls[i].Location as LocationCurve;
                XYZ p1 = curve.Curve.GetEndPoint(0);
                XYZ p2 = curve.Curve.GetEndPoint(1);
                Line line = Line.CreateBound(p1 + pointsOffset[i], p2 + pointsOffset[i+1]);
                footPrint.Append(line);
            }

            ModelCurveArray footPrintToModelCurveMapping = new ModelCurveArray();
            FootPrintRoof footprintRoof = doc.Create.NewFootPrintRoof(footPrint, levelRoof, roofType, out footPrintToModelCurveMapping);

            //ModelCurveArrayIterator iterator = footPrintToModelCurveMapping.ForwardIterator();
            //iterator.Reset();
            //while (iterator.MoveNext())
            //{
            //    ModelCurve modelCurve = iterator.Current as ModelCurve;
            //    footprintRoof.set_DefinesSlope(modelCurve, true);
            //    footprintRoof.set_SlopeAngle(modelCurve, 0.5);
            //}

            foreach (ModelCurve m in footPrintToModelCurveMapping)
            {
                footprintRoof.set_DefinesSlope(m, true);
                footprintRoof.set_SlopeAngle(m, 0.5);
            }
        }

        private void CreateWindow(Document doc, Level level, Wall wall)
        {
            FamilySymbol windowType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Windows)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0610 x 1220 мм"))
                .Where(x => x.FamilyName.Equals("Фиксированные"))
                .FirstOrDefault();

            LocationCurve locationCurve = wall.Location as LocationCurve;
            XYZ pointBegin = locationCurve.Curve.GetEndPoint(0);
            XYZ pointEnd = locationCurve.Curve.GetEndPoint(1);
            XYZ pointInsert = (pointBegin + pointEnd) / 2;

            XYZ offsetWindowByAxisZ = new XYZ(0, 0, UnitUtils.ConvertToInternalUnits(1000, UnitTypeId.Millimeters));
            pointInsert += offsetWindowByAxisZ;

            if (!windowType.IsActive)
                windowType.Activate();

            FamilyInstance window = doc.Create.NewFamilyInstance(pointInsert, windowType, wall, StructuralType.NonStructural);

            //вариант со смещение уже созданного окна
            //LocationPoint windowLocation = window.Location as LocationPoint;
            //double offsetWindowByAxisZinMillimeters = 1000;
            //XYZ movePoint = windowLocation.Point + new XYZ (0,0, UnitUtils.ConvertToInternalUnits(offsetWindowByAxisZinMillimeters, UnitTypeId.Millimeters));
            //ElementTransformUtils.MoveElement(doc, window.Id, movePoint);
        }

        private void CreateDoor(Document doc, Level level, Wall wall)
        {
            FamilySymbol doorType = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .OfCategory(BuiltInCategory.OST_Doors)
                .OfType<FamilySymbol>()
                .Where(x => x.Name.Equals("0762 x 2032 мм"))
                .Where(x => x.FamilyName.Equals("Одиночные-Щитовые"))
                .FirstOrDefault();

            LocationCurve locationCurve =  wall.Location as LocationCurve;
            XYZ pointBegin = locationCurve.Curve.GetEndPoint(0);
            XYZ pointEnd = locationCurve.Curve.GetEndPoint(1);
            XYZ pointInsert = (pointBegin + pointEnd) / 2;

            if (!doorType.IsActive)
                doorType.Activate();

            doc.Create.NewFamilyInstance(pointInsert, doorType, wall, StructuralType.NonStructural);
        }

        public List<Wall> CreateWalls(Document doc, Level downLevel, Level upLevel, List<XYZ> pointsOfWalls)
        {
            List<Wall> walls = new List<Wall>();

            for (int i = 0; i < pointsOfWalls.Count - 1; i++)
            {             
                Line line = Line.CreateBound(pointsOfWalls[i], pointsOfWalls[i + 1]);
                Wall wall = Wall.Create(doc, line, downLevel.Id, false);
                wall.get_Parameter(BuiltInParameter.WALL_HEIGHT_TYPE).Set(upLevel.Id);
                walls.Add(wall);
            }

            return walls;
        }

        public List<Level> FindAllLevels(Document document)
        {
            var levels = new FilteredElementCollector(document)
                .OfClass(typeof(Level))
                .OfType<Level>()
                .ToList();

            return levels;
        }

        public Level FindLevel (List<Level> levels, string nameLevel)
        {
            Level level = levels
                .Where(x => x.Name.Equals(nameLevel))
                .FirstOrDefault();

            return level;
        }

        public List<XYZ> GetPointsForWall(double widhtInMillimetr, double depthInMillimetr)
        {
            double widht = UnitUtils.ConvertToInternalUnits(widhtInMillimetr, UnitTypeId.Millimeters);
            double depth = UnitUtils.ConvertToInternalUnits(depthInMillimetr, UnitTypeId.Millimeters);

            double dx = widht / 2;
            double dy = depth / 2;

            List<XYZ> points = new List<XYZ>();
            points.Add(new XYZ(-dx, -dy, 0));
            points.Add(new XYZ(dx, -dy, 0));
            points.Add(new XYZ(dx, dy, 0));
            points.Add(new XYZ(-dx, dy, 0));
            points.Add(new XYZ(-dx, -dy, 0));

            return points;
        }
    }
}
