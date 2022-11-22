using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Architecture;
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



            using (Transaction transaction = new Transaction(doc, "Create"))
            {
                transaction.Start();

                walls = CreateWalls(doc, level1, level2, points);

                transaction.Commit();
            }

            return Result.Succeeded;
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
