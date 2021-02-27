Public Class TopoPickFilter
    Implements ISelectionFilter

    Public Function AllowElement(elem As Element) As Boolean Implements ISelectionFilter.AllowElement
        If elem.Category Is Nothing Then
            Return False
        End If
        If elem.Category.Id.IntegerValue = CInt(BuiltInCategory.OST_Topography) Then
            Return True
        End If
        Return False
        'Return (elem.Category.Id.IntegerValue.Equals(CInt(BuiltInCategory.OST_Topography)))
    End Function

    Public Function AllowReference(reference As Reference, position As XYZ) As Boolean Implements ISelectionFilter.AllowReference
        Return False
    End Function
End Class
