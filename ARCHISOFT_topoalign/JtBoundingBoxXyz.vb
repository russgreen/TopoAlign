Imports System.Collections.Generic
Imports Autodesk.Revit.DB

''' <summary>
''' A bounding box for a collection of XYZ instances.
''' The components of a tuple are read-only and cannot be changed after instantiation, so I cannot use that easily.
''' The components of an XYZ are read-only and cannot be changed except by re-instantiation, so I cannot use that easily either.
''' </summary>
Public Class JtBoundingBoxXyz
    ' : Tuple<XYZ, XYZ>
    ''' <summary>
    ''' Array of six doubles, first three for 
    ''' minimum, last three for maximum values.
    ''' </summary>
    Private _a As Double()

    ''' <summary>
    ''' Initialise to infinite values.
    ''' </summary>
    Public Sub New()
        ': base(
        '  new XYZ( double.MaxValue, double.MaxValue, double.MaxValue ),
        '  new XYZ( double.MinValue, double.MinValue, double.MinValue ) )
        'Min = new XYZ( double.MaxValue, double.MaxValue, double.MaxValue );
        'Max = new XYZ( double.MinValue, double.MinValue, double.MinValue );
        _a = New Double(5) {}
        _a(0) = InlineAssignHelper(_a(1), InlineAssignHelper(_a(2), Double.MaxValue))
        _a(3) = InlineAssignHelper(_a(4), InlineAssignHelper(_a(5), Double.MinValue))
    End Sub

    ''' <summary>
    ''' Return current lower left corner.
    ''' </summary>
    Public ReadOnly Property Min() As XYZ
        Get
            Return New XYZ(_a(0), _a(1), _a(2))
        End Get
    End Property

    ''' <summary>
    ''' Return current upper right corner.
    ''' </summary>
    Public ReadOnly Property Max() As XYZ
        Get
            Return New XYZ(_a(3), _a(4), _a(5))
        End Get
    End Property

    Public ReadOnly Property MidPoint() As XYZ
        Get
            Return 0.5 * (Min + Max)
        End Get
    End Property


    ''' <summary>
    ''' Expand bounding box to contain 
    ''' the given new point.
    ''' </summary>
    Public Sub ExpandToContain(p As XYZ)
        If p.X < _a(0) Then
            _a(0) = p.X
        End If
        If p.Y < _a(1) Then
            _a(1) = p.Y
        End If
        If p.Z < _a(2) Then
            _a(2) = p.Z
        End If
        If p.X > _a(3) Then
            _a(3) = p.X
        End If
        If p.Y > _a(4) Then
            _a(4) = p.Y
        End If
        If p.Z > _a(5) Then
            _a(5) = p.Z
        End If

    End Sub

    Public Shared Function GetBoundingBoxOf(xyzarraylist As List(Of List(Of XYZ))) As JtBoundingBoxXyz

        Dim bb As New JtBoundingBoxXyz()

        For Each a As List(Of XYZ) In xyzarraylist
            For Each p As XYZ In a
                bb.ExpandToContain(p)
            Next
        Next
        Return bb
    End Function
    Private Shared Function InlineAssignHelper(Of T)(ByRef target As T, value As T) As T
        target = value
        Return value
    End Function
End Class
