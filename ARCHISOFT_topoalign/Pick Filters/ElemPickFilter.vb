Public Class ElemPickFilter
    Implements ISelectionFilter

    Public Function AllowElement(elem As Element) As Boolean Implements ISelectionFilter.AllowElement
        Select Case elem.Category.Id.IntegerValue
            Case CInt(BuiltInCategory.OST_Topography) : Return True
            Case CInt(BuiltInCategory.OST_BuildingPad) : Return True
            Case CInt(BuiltInCategory.OST_Floors) : Return True
            Case CInt(BuiltInCategory.OST_Roofs) : Return True
            Case CInt(BuiltInCategory.OST_Site) : Return True
            Case CInt(BuiltInCategory.OST_GenericModel) : Return True
                'Case CInt(BuiltInCategory.OST_Stairs) : Return True
                'Case CInt(BuiltInCategory.OST_Ramps) : Return True
            Case CInt(BuiltInCategory.OST_Furniture) : Return True
            Case CInt(BuiltInCategory.OST_Mass) : Return True
            Case CInt(BuiltInCategory.OST_Planting) : Return True
            Case CInt(BuiltInCategory.OST_Walls) : Return True

            Case Else : Return False
        End Select

    End Function

    Public Function AllowReference(reference As Reference, position As XYZ) As Boolean Implements ISelectionFilter.AllowReference
        Return False
    End Function
End Class
