Public Class LinePickFilter
    Implements ISelectionFilter

    Public Function AllowElement(elem As Element) As Boolean Implements ISelectionFilter.AllowElement
        ' Convert the element to a ModelLine
        Dim line As ModelLine = TryCast(elem, ModelLine)
        Dim curve As ModelCurve = TryCast(elem, ModelCurve)
        ' line is null if the element is not a model line
        If line Is Nothing And curve Is Nothing Then
            Return False
        End If
        ' return true if the line is a model line
        Return True
    End Function

    Public Function AllowReference(reference As Reference, position As XYZ) As Boolean Implements ISelectionFilter.AllowReference
        Return False
    End Function
End Class
