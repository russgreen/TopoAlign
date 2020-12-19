Imports Microsoft.Win32
Imports System.ComponentModel

Public Class Settings
    Implements INotifyPropertyChanged

    Public Event PropertyChanged(sender As Object, e As PropertyChangedEventArgs) Implements INotifyPropertyChanged.PropertyChanged

    Private m_SingleElement As Boolean
    Private m_CleanTopoPoints As Boolean
    Private m_TopFace As Boolean
    Private m_DivideEdgeDistance As Decimal
    Private m_VerticalOffset As Decimal

    Public Property SingleElement() As Boolean
        Get
            Return m_SingleElement
        End Get
        Set(ByVal value As Boolean)
            m_SingleElement = value
        End Set
    End Property

    Public Property CleanTopoPoints() As Boolean
        Get
            Return m_CleanTopoPoints
        End Get
        Set(ByVal value As Boolean)
            m_CleanTopoPoints = value
        End Set
    End Property

    Public Property TopFace() As Boolean
        Get
            Return m_TopFace
        End Get
        Set(ByVal value As Boolean)
            m_TopFace = value
        End Set
    End Property

    Public Property DivideEdgeDistance() As Decimal
        Get
            Return m_DivideEdgeDistance
        End Get
        Set(ByVal value As Decimal)
            m_DivideEdgeDistance = value
        End Set
    End Property

    Public Property VerticalOffset() As Decimal
        Get
            Return m_VerticalOffset
        End Get
        Set(ByVal value As Decimal)
            m_VerticalOffset = value
        End Set
    End Property

    Public Sub New()
        m_SingleElement = True
        m_CleanTopoPoints = True
        m_TopFace = True
        m_DivideEdgeDistance = Convert.ToDecimal(16.4041994750656) '5000
        m_VerticalOffset = Convert.ToDecimal(0.164041994750656) '50
    End Sub

    Public Sub LoadSettings()
        Try
            If My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign") IsNot Nothing Then
                With My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign")
                    m_SingleElement = Convert.ToBoolean(.GetValue("SingleElement", m_SingleElement))
                    m_CleanTopoPoints = Convert.ToBoolean(.GetValue("CleanTopoPoints", m_CleanTopoPoints))
                    m_TopFace = Convert.ToBoolean(.GetValue("TopFace", m_TopFace))
                    m_DivideEdgeDistance = Convert.ToDecimal(.GetValue("DivideEdgeDistance", m_DivideEdgeDistance))
                    m_VerticalOffset = Convert.ToDecimal(.GetValue("VerticalOffset", m_VerticalOffset))
                End With
            End If
        Catch ex As Exception

        Finally
            My.Computer.Registry.CurrentUser.Close()
        End Try
    End Sub

    Public Sub SaveSettings()
        If My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign") Is Nothing Then
            My.Computer.Registry.CurrentUser.CreateSubKey("Software\Archisoft\TopoAlign")
        End If

        With My.Computer.Registry.CurrentUser.OpenSubKey("Software\Archisoft\TopoAlign", True)
            .SetValue("SingleElement", m_SingleElement, RegistryValueKind.String)
            .SetValue("CleanTopoPoints", m_CleanTopoPoints, RegistryValueKind.String)
            .SetValue("TopFace", m_TopFace, RegistryValueKind.String)
            .SetValue("DivideEdgeDistance", m_DivideEdgeDistance, RegistryValueKind.String)
            .setValue("VerticalOffset", m_VerticalOffset, RegistryValueKind.String)
        End With
    End Sub
End Class
