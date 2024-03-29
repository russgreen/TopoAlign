﻿using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;

namespace TopoAlign;

public class FacePickFilter : ISelectionFilter
{
    private Document m_doc = null;

    public FacePickFilter(Document doc)
    {
        m_doc = doc;
    }

    public bool AllowElement(Element elem)
    {
        return true;
    }

    public bool AllowReference(Reference reference, XYZ position)
    {
        var geoObject = m_doc.GetElement(reference).GetGeometryObjectFromReference(reference);
        return geoObject is object && geoObject is Face;
    }
}
