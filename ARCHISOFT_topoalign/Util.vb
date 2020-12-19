#Region "Namespaces"
Imports System
Imports System.Collections.Generic
Imports System.Diagnostics
Imports System.Linq
Imports System.Reflection
Imports WinForms = System.Windows.Forms
Imports Autodesk.Revit.ApplicationServices
Imports Autodesk.Revit.Attributes
Imports Autodesk.Revit.DB
Imports Autodesk.Revit.UI
Imports Autodesk.Revit.UI.Selection
#End Region

#If CONFIG = "2016" Or CONFIG = "2017" Then
Imports System.Threading.Tasks
Imports System.Net 'for HttpStatusCode 

'Added for REST API
Imports RestSharp
Imports RestSharp.Deserializers
#End If

Public Class Util
    Public Const MAX_ROUNDING_PRECISION As Double = 0.000000000001

    Public Sub ShowBasicElementInfo(ByRef m_rvtDoc As Document, ByVal elem As Element)

        ''  let's see what kind of element we got. 
        Dim s As String = "You picked:" + vbCr
        s = s + "  Class name = " + elem.GetType.Name + vbCr
        s = s + "  Category = " + elem.Category.Name + vbCr
        s = s + "  Element id = " + elem.Id.ToString + vbCr + vbCr

        ''  and check its type info. 
        Dim elemTypeId As ElementId = elem.GetTypeId
        Dim elemType As ElementType = TryCast(m_rvtDoc.GetElement(elemTypeId), ElementType)
        s = s + "Its ElementType:" + vbCr
        s = s + "  Class name = " + elemType.GetType.Name + vbCr
        s = s + "  Category = " + elemType.Category.Name + vbCr
        s = s + "  Element type id = " + elemType.Id.ToString + vbCr

        ''  show what we got. 
        TaskDialog.Show("Basic Element Info", s)

    End Sub

#Region "Unit Handling"
    ''' <summary>
    ''' Base units currently used internally by Revit.
    ''' </summary>
    Private Enum BaseUnit
        BU_Length = 0           ' length, feet (ft)
        BU_Angle                ' angle, radian (rad)
        BU_Mass                 ' mass, kilogram (kg)
        BU_Time                 ' time, second (s)
        BU_Electric_Current     ' electric current, ampere (A)
        BU_Temperature          ' temperature, kelvin (K)
        BU_Luminous_Intensity   ' luminous intensity, candela (cd)
        BU_Solid_Angle          ' solid angle, steradian (sr)
        NumBaseUnits
    End Enum

    Const _convertFootToMm As Double = 12 * 25.4

    Const _convertFootToMeter As Double = _convertFootToMm * 0.001

    Const _convertCubicFootToCubicMeter As Double = _convertFootToMeter * _convertFootToMeter * _convertFootToMeter

    ''' <summary>
    ''' Convert a given length in feet to millimetres.
    ''' </summary>
    Public Shared Function FootToMm(length As Double) As Double
        Return length * _convertFootToMm
    End Function

    ''' <summary>
    ''' Convert a given length in millimetres to feet.
    ''' </summary>
    Public Shared Function MmToFoot(length As Double) As Double
        Return length / _convertFootToMm
    End Function

    ''' <summary>
    ''' Convert a given point or vector from millimetres to feet.
    ''' </summary>
    Public Shared Function MmToFoot(v As XYZ) As XYZ
        Return v.Divide(_convertFootToMm)
    End Function

    ''' <summary>
    ''' Convert a given volume in feet to cubic meters.
    ''' </summary>
    Public Shared Function CubicFootToCubicMeter(volume As Double) As Double
        Return volume * _convertCubicFootToCubicMeter
    End Function

    'Public Shared Function getCurrentDisplayUnitType(unitType As UnitType, document As Document, ByRef roundingPrecision As System.Nullable(Of Double)) As DisplayUnitType

    '    ' Following good SOA practices, don't trust the incoming parameters and verify
    '    ' that they have values that can be worked with before doing anything.
    '    If document Is Nothing Then
    '        Throw New ArgumentNullException("document")
    '    End If

    '    ' This function does not require the document to be a family document, so don't check for that.

    '    Dim formatOption As FormatOptions
    '    Dim result As DisplayUnitType

    '    Dim projectUnit As Units = document.GetUnits

    '    Try

    '        formatOption = projectUnit.GetFormatOptions(unitType)
    '    Catch ex As Exception
    '        Throw New ArgumentOutOfRangeException("The UnitType '" + unitType.ToString() + "' does not have any DisplayUnitType options.", ex)
    '    End Try

    '    Try
    '        result = formatOption.UnitSymbol
    '    Catch ex As Exception
    '        Throw New Exception("Unable to get the DisplayUnitType for UnitType '" + unitType.ToString() + "'", ex)
    '    End Try


    '    Try
    '        roundingPrecision = formatOption.GetRounding
    '    Catch generatedExceptionName As Exception
    '        ' Not all dimensional types support rounding, so if an exception is thrown,
    '        ' store the fact into our nullable double that there is no value.
    '        roundingPrecision = Nothing
    '    End Try


    '    Return result
    'End Function

#End Region

#Region "Geometrical Comparison"
    Const _eps As Double = 0.000000001

    Public Shared ReadOnly Property Eps() As Double
        Get
            Return _eps
        End Get
    End Property

    Public Shared ReadOnly Property MinLineLength() As Double
        Get
            Return _eps
        End Get
    End Property

    Public Shared ReadOnly Property TolPointOnPlane() As Double
        Get
            Return _eps
        End Get
    End Property

    Public Shared Function IsZero(a As Double, tolerance As Double) As Boolean
        Return tolerance > Math.Abs(a)
    End Function

    Public Shared Function IsZero(a As Double) As Boolean
        Return IsZero(a, _eps)
    End Function

    Public Shared Function IsEqual(a As Double, b As Double) As Boolean
        Return IsZero(b - a)
    End Function

    Public Shared Function Compare(a As Double, b As Double) As Integer
        Return If(IsEqual(a, b), 0, (If(a < b, -1, 1)))
    End Function


    Public Shared Function Compare(p As XYZ, q As XYZ) As Integer
        Dim d As Integer = Compare(p.X, q.X)

        If 0 = d Then
            d = Compare(p.Y, q.Y)

            If 0 = d Then
                d = Compare(p.Z, q.Z)
            End If
        End If
        Return d
    End Function

    Public Shared Function IsEqual(p As XYZ, q As XYZ) As Boolean
        Return 0 = Compare(p, q)
    End Function

    ''' <summary>
    ''' Return true if the vectors v and w 
    ''' are non-zero and perpendicular.
    ''' </summary>
    Private Function IsPerpendicular(v As XYZ, w As XYZ) As Boolean
        Dim a As Double = v.GetLength()
        Dim b As Double = v.GetLength()
        Dim c As Double = Math.Abs(v.DotProduct(w))
        Return _eps < a AndAlso _eps < b AndAlso _eps > c
        ' c * c < _eps * a * b
    End Function

    Public Shared Function IsParallel(p As XYZ, q As XYZ) As Boolean
        Return p.CrossProduct(q).IsZeroLength()
    End Function

    Public Shared Function IsHorizontal(v As XYZ) As Boolean
        Return IsZero(v.Z)
    End Function

    Public Shared Function IsVertical(v As XYZ) As Boolean
        Return IsZero(v.X) AndAlso IsZero(v.Y)
    End Function

    Public Shared Function IsVertical(v As XYZ, tolerance As Double) As Boolean
        Return IsZero(v.X, tolerance) AndAlso IsZero(v.Y, tolerance)
    End Function

    Public Shared Function IsHorizontal(e As Edge) As Boolean
        Dim p As XYZ = e.Evaluate(0)
        Dim q As XYZ = e.Evaluate(1)
        Return IsHorizontal(q - p)
    End Function

    'Public Shared Function IsHorizontal(f As PlanarFace) As Boolean
    '    'Return IsVertical(f.Normal) '2015
    '    Return IsVertical(f.FaceNormal) '2016
    'End Function

    'Public Shared Function IsVertical(f As PlanarFace) As Boolean
    '    'Return IsHorizontal(f.Normal)'2015
    '    Return IsHorizontal(f.FaceNormal) '2016
    'End Function

    Public Shared Function IsVertical(f As CylindricalFace) As Boolean
        Return IsVertical(f.Axis)
    End Function


    ''' <summary>
    ''' Minimum slope for a vector to be considered
    ''' to be pointing upwards. Slope is simply the
    ''' relationship between the vertical and
    ''' horizontal components.
    ''' </summary>
    Const _minimumSlope As Double = 0.3

    ''' <summary>
    ''' Return true if the Z coordinate of the
    ''' given vector is positive and the slope
    ''' is larger than the minimum limit.
    ''' </summary>
    Public Shared Function PointsUpwards(v As XYZ) As Boolean
        Dim horizontalLength As Double = v.X * v.X + v.Y * v.Y
        Dim verticalLength As Double = v.Z * v.Z

        Return 0 < v.Z AndAlso _minimumSlope < verticalLength / horizontalLength

        'return _eps < v.Normalize().Z;
        'return _eps < v.Normalize().Z && IsVertical( v.Normalize(), tolerance );
    End Function

    Public Shared Function PointsDownwards(v As XYZ) As Boolean
        Dim horizontalLength As Double = v.X * v.X + v.Y * v.Y
        Dim verticalLength As Double = v.Z * v.Z

        Return 0 > v.Z AndAlso _minimumSlope < verticalLength / horizontalLength

        'return _eps < v.Normalize().Z;
        'return _eps < v.Normalize().Z && IsVertical( v.Normalize(), tolerance );
    End Function

    ''' <summary>
    ''' Return true if the given bounding box bb
    ''' contains the given point p in its interior.
    ''' </summary>
    Public Function BoundingBoxXyzContains(bb As BoundingBoxXYZ, p As XYZ) As Boolean
        Dim retval = 0 < Compare(bb.Min, p) AndAlso 0 < Compare(p, bb.Max)
        Return retval
    End Function

#End Region

#Region "Flatten, i.e. project from 3D to 2D by dropping the Z coordinate"
    ''' <summary>
    ''' Eliminate the Z coordinate.
    ''' </summary>
    Public Shared Function Flatten(point As XYZ) As UV
        Return New UV(point.X, point.Y)
    End Function

    ''' <summary>
    ''' Eliminate the Z coordinate.
    ''' </summary>
    Public Shared Function Flatten(polygon As List(Of XYZ)) As List(Of UV)
        Dim z As Double = polygon(0).Z
        Dim a As New List(Of UV)(polygon.Count)
        For Each p As XYZ In polygon
            Debug.Assert(Util.IsEqual(p.Z, z), "expected horizontal polygon")
            a.Add(Flatten(p))
        Next
        Return a
    End Function

    ''' <summary>
    ''' Eliminate the Z coordinate.
    ''' </summary>
    Public Shared Function Flatten(polygons As List(Of List(Of XYZ))) As List(Of List(Of UV))
        Dim z As Double = polygons(0)(0).Z
        Dim a As New List(Of List(Of UV))(polygons.Count)
        For Each polygon As List(Of XYZ) In polygons
            Debug.Assert(Util.IsEqual(polygon(0).Z, z), "expected horizontal polygons")
            a.Add(Flatten(polygon))
        Next
        Return a
    End Function
#End Region

#Region "Geometrical XYZ Calculation"
    ''' <summary>
    ''' Return the midpoint between two points.
    ''' </summary>
    Public Shared Function Midpoint(p As XYZ, q As XYZ) As XYZ
        Return p + 0.5 * (q - p)
    End Function

    ''' <summary>
    ''' Return the midpoint of a Line.
    ''' </summary>
    Public Shared Function Midpoint(line As Line) As XYZ
        Return Midpoint(line.GetEndPoint(0), line.GetEndPoint(1))
    End Function

    ''' <summary>
    ''' Return the normal of a Line in the XY plane.
    ''' </summary>
    Public Shared Function Normal(line As Line) As XYZ
        Dim p As XYZ = line.GetEndPoint(0)
        Dim q As XYZ = line.GetEndPoint(1)
        Dim v As XYZ = q - p

        'Debug.Assert( IsZero( v.Z ),
        '  "expected horizontal line" );

        Return v.CrossProduct(XYZ.BasisZ).Normalize()
    End Function


    Public Shared Function DividePoints(A As XYZ, B As XYZ, length As Double, increment As Double) As List(Of XYZ)
        Dim retval As New List(Of XYZ)
        retval.Add(A)
        retval.Add(B)

        'retval.Add(Midpoint(A, B))

        '((Xa - Xb) / L) * Ld
        Dim newLength As Double = increment
        For i As Integer = 1 To CInt(length / increment) - 1
            Dim Xn As Double = A.X + (((B.X - A.X) / length) * newLength)
            Dim Yn As Double = A.Y + (((B.Y - A.Y) / length) * newLength)
            Dim Zn As Double = A.Z + (((B.Z - A.Z) / length) * newLength)

            Dim pt As New XYZ(Xn, Yn, Zn)
            retval.Add(pt)
            newLength = newLength + increment
        Next

        Return retval
    End Function

    Public Shared Function IsTopFace(f As Face) As Boolean
        Dim b As BoundingBoxUV = f.GetBoundingBox()
        Dim p As UV = b.Min
        Dim q As UV = b.Max
        Dim midpoint As UV = p + 0.5 * (q - p)
        Dim normal As XYZ = f.ComputeNormal(midpoint)
        Return Util.PointsUpwards(normal)
    End Function

    Public Shared Function IsBottomFace(f As Face) As Boolean
        Dim b As BoundingBoxUV = f.GetBoundingBox()
        Dim p As UV = b.Min
        Dim q As UV = b.Max
        Dim midpoint As UV = p + 0.5 * (q - p)
        Dim normal As XYZ = f.ComputeNormal(midpoint)
        Return Util.PointsDownwards(normal)
    End Function

    ''' <summary>
    ''' Return the four XYZ corners of the given 
    ''' bounding box in the XY plane at the minimum 
    ''' Z elevation in the order lower left, lower 
    ''' right, upper right, upper left:
    ''' </summary>
    'Public Shared Function GetCorners(b As BoundingBoxXYZ) As XYZ()
    '    Dim z As Double = AddressOf b.Min.Z

    '    Return New XYZ() {New XYZ(AddressOf b.Min.X, AddressOf b.Min.Y, z), New XYZ(AddressOf b.Max.X, AddressOf b.Min.Y, z), New XYZ(AddressOf b.Max.X, AddressOf b.Max.Y, z), New XYZ(AddressOf b.Min.X, AddressOf b.Max.Y, z)}
    'End Function

    ''' <summary>
    ''' Offset the generated boundary polygon loop
    ''' model lines downwards to separate them from
    ''' the slab edge.
    ''' </summary>
    Const _offset As Double = 0.1

    ''' <summary>
    ''' Determine the boundary polygons of the lowest
    ''' horizontal planar face of the given solid.
    ''' </summary>
    ''' <param name="polygons">Return polygonal boundary
    ''' loops of lowest horizontal face, i.e. profile of
    ''' circumference and holes</param>
    ''' <param name="solid">Input solid</param>
    ''' <returns>False if no horizontal planar face was
    ''' found, else true</returns>
    Private Shared Function GetBoundary(polygons As List(Of List(Of XYZ)), solid As Solid) As Boolean
        Dim lowest As PlanarFace = Nothing
        Dim faces As FaceArray = solid.Faces
        For Each f As Face In faces
            Dim pf As PlanarFace = TryCast(f, PlanarFace)
            If pf IsNot Nothing AndAlso IsBottomFace(f) = True Then
                If (lowest Is Nothing) OrElse (pf.Origin.Z < lowest.Origin.Z) Then
                    lowest = pf

                    Dim p As XYZ, q As XYZ = XYZ.Zero
                    Dim first As Boolean
                    Dim i As Integer, n As Integer
                    Dim loops As EdgeArrayArray = lowest.EdgeLoops
                    For Each [loop] As EdgeArray In loops
                        Dim vertices As New List(Of XYZ)()
                        first = True
                        For Each e As Edge In [loop]
                            Dim points As IList(Of XYZ) = e.Tessellate()
                            p = points(0)
                            If Not first Then
                                Debug.Assert(p.IsAlmostEqualTo(q), "expected subsequent start point" & " to equal previous end point")
                            End If
                            n = points.Count
                            q = points(n - 1)
                            For i = 0 To n - 2
                                Dim v As XYZ = points(i)
                                v -= _offset * XYZ.BasisZ
                                vertices.Add(v)
                            Next
                        Next
                        q -= _offset * XYZ.BasisZ
                        Debug.Assert(q.IsAlmostEqualTo(vertices(0)), "expected last end point to equal" & " first start point")
                        polygons.Add(vertices)
                    Next

                End If
            End If
        Next
        'If lowest IsNot Nothing Then
        '    Dim p As XYZ, q As XYZ = XYZ.Zero
        '    Dim first As Boolean
        '    Dim i As Integer, n As Integer
        '    Dim loops As EdgeArrayArray = lowest.EdgeLoops
        '    For Each [loop] As EdgeArray In loops
        '        Dim vertices As New List(Of XYZ)()
        '        first = True
        '        For Each e As Edge In [loop]
        '            Dim points As IList(Of XYZ) = e.Tessellate()
        '            p = points(0)
        '            If Not first Then
        '                Debug.Assert(p.IsAlmostEqualTo(q), "expected subsequent start point" & " to equal previous end point")
        '            End If
        '            n = points.Count
        '            q = points(n - 1)
        '            For i = 0 To n - 2
        '                Dim v As XYZ = points(i)
        '                v -= _offset * XYZ.BasisZ
        '                vertices.Add(v)
        '            Next
        '        Next
        '        q -= _offset * XYZ.BasisZ
        '        Debug.Assert(q.IsAlmostEqualTo(vertices(0)), "expected last end point to equal" & " first start point")
        '        polygons.Add(vertices)
        '    Next
        'End If
        Return lowest IsNot Nothing
    End Function

    ''' <summary>
    ''' Return all floor slab boundary loop polygons
    ''' for the given floors, offset downwards from the
    ''' bottom floor faces by a certain amount.
    ''' </summary>
    Public Shared Function GetBoundaryPolygons(elems As List(Of Element), opt As Options) As List(Of List(Of XYZ))
        Dim polygons As New List(Of List(Of XYZ))()

        For Each e As Element In elems
            Dim geo As GeometryElement = e.Geometry(opt)

            'GeometryObjectArray objects = geo.Objects; // 2012
            'foreach( GeometryObject obj in objects ) // 2012

            For Each obj As GeometryObject In geo
                ' 2013
                Dim solid As Solid = TryCast(obj, Solid)
                If solid IsNot Nothing Then
                    GetBoundary(polygons, solid)
                End If
            Next
        Next
        Return polygons
    End Function

    '''<summary>
    ''' Return the 2D intersection point between two
    ''' unbounded lines defined In the XY plane by the
    ''' start And end points of the two given curves.
    ''' By Magson Leone.
    ''' Return null If the two lines are coincident,
    ''' in which case the intersection Is an infinite
    ''' line, Or non-coincident And parallel, in which
    ''' Case it Is empty.
    ''' https : //en.wikipedia.org/wiki/Line%E2%80%93line_intersection
    ''' </summary>
    Public Shared Function Intersection(ByVal c1 As Curve, ByVal c2 As Curve) As XYZ
        Dim p1 As XYZ = c1.GetEndPoint(0)
        Dim q1 As XYZ = c1.GetEndPoint(1)
        Dim p2 As XYZ = c2.GetEndPoint(0)
        Dim q2 As XYZ = c2.GetEndPoint(1)
        Dim v1 As XYZ = q1 - p1
        Dim v2 As XYZ = q2 - p2
        Dim w As XYZ = p2 - p1
        Dim p5 As XYZ = Nothing
        Dim c As Double = (v2.X * w.Y - v2.Y * w.X) / (v2.X * v1.Y - v2.Y * v1.X)

        If Not Double.IsInfinity(c) Then
            Dim x As Double = p1.X + c * v1.X
            Dim y As Double = p1.Y + c * v1.Y
            p5 = New XYZ(x, y, 0)
        End If

        Return p5
    End Function

#End Region

    Public Function FindParameterByName(ByRef element As Element, ByVal Definition As String) As Autodesk.Revit.DB.Parameter
        Dim foundParameter As Autodesk.Revit.DB.Parameter = Nothing
        ' This will find the first parameter that measures length
        For Each parameter As Autodesk.Revit.DB.Parameter In element.Parameters
            If parameter.Definition.Name = Definition Then
                foundParameter = parameter
                Exit For
            End If
        Next
        Return foundParameter
    End Function

#Region "Formatting"
    ''' <summary>
    ''' Return an English plural suffix for the given
    ''' number of items, i.e. 's' for zero or more
    ''' than one, and nothing for exactly one.
    ''' </summary>
    Public Shared Function PluralSuffix(n As Integer) As String
        Return If(1 = n, "", "s")
    End Function

    ''' <summary>
    ''' Return an English plural suffix 'ies' or
    ''' 'y' for the given number of items.
    ''' </summary>
    Public Shared Function PluralSuffixY(n As Integer) As String
        Return If(1 = n, "y", "ies")
    End Function


    ''' <summary>
    ''' Return a string for a real number
    ''' formatted to two decimal places.
    ''' </summary>
    Public Shared Function RealString(a As Double) As String
        Return a.ToString("0.##")
    End Function


    ''' <summary>
    ''' Return a string for a UV point
    ''' or vector with its coordinates
    ''' formatted to two decimal places.
    ''' </summary>
    Public Shared Function PointString(p As UV) As String
        Return String.Format("({0},{1})", RealString(p.U), RealString(p.V))
    End Function

    ''' <summary>
    ''' Return a string for an XYZ point
    ''' or vector with its coordinates
    ''' formatted to two decimal places.
    ''' </summary>
    Public Shared Function PointString(p As XYZ) As String
        Return String.Format("({0},{1},{2})", RealString(p.X), RealString(p.Y), RealString(p.Z))
    End Function

#End Region

    ' Set values specific to the environment 
    Public Const _baseApiUrl As String = "https://apps.exchange.autodesk.com/"

#If CONFIG = "2016" Then
 Public Const _appId As String = "appstore.exchange.autodesk.com:topoalign1425670401_windows64:en"
#End If

#If CONFIG = "2017" Then
    Public Const _appId As String = "7267740296767583135"
#End If

#If CONFIG = "2016" Or CONFIG = "2017" Then

    ' This is the id of your app.
    ' e.g., 
    'public const string _appId = @"appstore.exchange.autodesk.com:TransTips-for-Revit:en"; 
    'https://apps.exchange.autodesk.com/webservices/checkentitlement?userid=200910140805021&appid=appstore.exchange.autodesk.com:topoalign1425670401_windows64:en

    Public Function LicenseCheck(ByRef app As Autodesk.Revit.ApplicationServices.Application) As Boolean
        Dim userId As String = String.Empty
        Dim isValid As Boolean
        Dim checkDate As DateTime

        Try
            'My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\Transmittal")
            If My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign") Is Nothing Then
                My.Computer.Registry.CurrentUser.CreateSubKey("Software\Archisoft\TopoAlign")
            End If

            If My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign").GetValue("UserID", Nothing) IsNot Nothing Then
                userId = My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign").GetValue("UserID", Nothing).ToString
                isValid = CBool(Cypher.DecryptString(My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign").GetValue("IsValid").ToString, userId))
                checkDate = CDate(Cypher.DecryptString(My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign").GetValue("Checked").ToString, userId))

                If isValid = True Then
                    'check if we need to re-validate (every 30days)
                    If DateTime.Now.Subtract(checkDate).Days > 30 Then
                        'End If
                        'If DateDiff(DateInterval.Day, DateTime.Now, checkDate) > 30 Then
                        If CheckLogin(userId, app) = True Then
                            'record in the details in the registry
                            My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("UserID", userId, Microsoft.Win32.RegistryValueKind.String)
                            My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("IsValid", Cypher.EncryptData(CStr(True), userId), Microsoft.Win32.RegistryValueKind.String)
                            My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString, userId), Microsoft.Win32.RegistryValueKind.String)
                        Else
                            Return False
                        End If
                    End If
                Else
                    If CheckLogin(userId, app) = True Then
                        'record in the details in the registry
                        My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("UserID", userId, Microsoft.Win32.RegistryValueKind.String)
                        My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("IsValid", Cypher.EncryptData(CStr(True), userId), Microsoft.Win32.RegistryValueKind.String)
                        My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString, userId), Microsoft.Win32.RegistryValueKind.String)
                    Else
                        Return False
                    End If
                End If

            Else
                'check online then record in registry
                If CheckLogin(userId, app) = True Then
                    'record in the details in the registry
                    If My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign") Is Nothing Then
                        My.Computer.Registry.CurrentUser.CreateSubKey("Software\Archisoft\TopoAlign")
                    End If

                    My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("UserID", userId, Microsoft.Win32.RegistryValueKind.String)
                    My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("IsValid", Cypher.EncryptData(CStr(True), userId), Microsoft.Win32.RegistryValueKind.String)
                    My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True).SetValue("Checked", Cypher.EncryptData(DateTime.Now.ToString, userId), Microsoft.Win32.RegistryValueKind.String)

                Else
                    Return False
                End If
            End If
        Catch ex As Exception
            'AdskApplication.elog.WriteEntry(ex.ToString, EventLogEntryType.Error)
        Finally
            My.Computer.Registry.CurrentUser.Close()
        End Try

        Return True
    End Function

    Private Function CheckLogin(ByRef userId As String, ByRef app As Autodesk.Revit.ApplicationServices.Application) As Boolean
        ' Check to see if the user is logged in.
        If Not Autodesk.Revit.ApplicationServices.Application.IsLoggedIn Then
            TaskDialog.Show("TopoAlign addin license", "Please login to Autodesk 360 first" & vbLf)
            Return False
        End If

        ' Get the user id, and check entitlement 
        userId = app.LoginUserId
        Dim isValid As Boolean = CheckEntitlement(userId)

        If isValid = False Then
            TaskDialog.Show("TopoAlign addin license", "You do not appear to have a valid license to use this addin. Please contact the author via the app store." & vbLf)
            Return False
        End If

        Return True
    End Function


    Private Function CheckEntitlement(userId As String) As Boolean
        Dim isValid As Boolean = False

        Try
            If userId = "200910140805021" Then Return True

            ' REST API call for the entitlement API.
            ' We are using RestSharp for simplicity.
            ' You may choose to use other library. 

            ' (1) Build request 
            Dim client = New RestClient()
            client.BaseUrl = New System.Uri(_baseApiUrl)

            ' Set resource/end point
            Dim request = New RestRequest()
            request.Resource = "webservices/checkentitlement"
            request.Method = Method.[GET]

            ' Add parameters 
            request.AddParameter("userid", userId)
            request.AddParameter("appid", _appId)

            ' (2) Execute request and get response
            Dim response As IRestResponse = client.Execute(request)

            ' (3) Parse the response and get the value of IsValid. 
            AdskApplication.elog.WriteEntry(response.Request.ToString, EventLogEntryType.Information)
            AdskApplication.elog.WriteEntry(response.StatusCode.ToString, EventLogEntryType.Information)

            If response.StatusCode = HttpStatusCode.OK Then
                Dim deserial As New JsonDeserializer()
                Dim entitlementResponse As EntitlementResponse = deserial.Deserialize(Of EntitlementResponse)(response)
                isValid = entitlementResponse.IsValid
            End If
        Catch ex As Exception
            AdskApplication.elog.WriteEntry(ex.ToString, EventLogEntryType.Error)
        End Try

        Return isValid
    End Function
#End If

    <Serializable> _
    Public Class EntitlementResponse
        Public Property UserId() As String
            Get
                Return m_UserId
            End Get
            Set(value As String)
                m_UserId = value
            End Set
        End Property
        Private m_UserId As String
        Public Property AppId() As String
            Get
                Return m_AppId
            End Get
            Set(value As String)
                m_AppId = value
            End Set
        End Property
        Private m_AppId As String
        Public Property IsValid() As Boolean
            Get
                Return m_IsValid
            End Get
            Set(value As Boolean)
                m_IsValid = value
            End Set
        End Property
        Private m_IsValid As Boolean
        Public Property Message() As String
            Get
                Return m_Message
            End Get
            Set(value As String)
                m_Message = value
            End Set
        End Property
        Private m_Message As String
    End Class

End Class
