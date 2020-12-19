Public Class FacePickFilter
    Implements ISelectionFilter

    Private m_doc As Document = Nothing

    Public Sub New(ByVal doc As Document)
        m_doc = doc
    End Sub

    Private Function ISelectionFilter_AllowElement(elem As Element) As Boolean Implements ISelectionFilter.AllowElement
        Return True
    End Function

    Private Function ISelectionFilter_AllowReference(reference As Reference, position As XYZ) As Boolean Implements ISelectionFilter.AllowReference
        Dim geoObject As GeometryObject = m_doc.GetElement(reference).GetGeometryObjectFromReference(reference)
        Return geoObject IsNot Nothing AndAlso TypeOf geoObject Is PlanarFace
    End Function
End Class
