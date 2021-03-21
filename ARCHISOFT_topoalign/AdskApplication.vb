#Region "Imported Namespaces"
Imports Autodesk.Revit.ApplicationServices
Imports Autodesk.Revit.Attributes
Imports Autodesk.Revit.DB
Imports Autodesk.Revit.UI
Imports System.Reflection
Imports System.Windows.Media.Imaging
Imports System.IO
Imports System.Windows.Media
'Imports Microsoft.AppCenter
'Imports Microsoft.AppCenter.Crashes
#End Region

<Transaction(TransactionMode.Manual)> <Regeneration(RegenerationOption.Manual)> Class AdskApplication
    Implements IExternalApplication
    ''' <summary>
    ''' This method is called when Revit starts up before a 
    ''' document or default template is actually loaded.
    ''' </summary>
    ''' <param name="app">An object passed to the external 
    ''' application which contains the controlled application.</param>
    ''' <returns>Return the status of the external application. 
    ''' A result of Succeeded means that the external application started successfully. 
    ''' Cancelled can be used to signify a problem. If so, Revit informs the user that 
    ''' the external application failed to load and releases the internal reference.
    ''' </returns>

    Public Shared _cachedUiCtrApp As UIControlledApplication
    'Public Shared elog As New EventLogger

    '#If CONFIG = "2015" Then
    '    Public Shared elog As New EventLogger("2015")
    '#ElseIf CONFIG = "2016" Then
    '    Public Shared elog As New EventLogger("2016")
    '#End If

    Public Function OnStartup(
      ByVal app As UIControlledApplication) _
    As Result Implements IExternalApplication.OnStartup

        'AppCenter.LogLevel = LogLevel.Verbose
        'Crashes.ShouldAwaitUserConfirmation = Function()
        '                                          ' Build your own UI to ask for user consent here. SDK doesn't provide one by default.
        '                                          Dim dialog = New DialogUserConfirmation()

        '                                          If dialog.ShowDialog() = System.Windows.Forms.DialogResult.None Then
        '                                              Crashes.NotifyUserConfirmation(dialog.ClickResult)
        '                                          End If

        '                                          ' Return true if you built a UI for user consent and are waiting for user input on that custom UI, otherwise false.
        '                                          Return True
        '                                      End Function

        'AppCenter.Start("c26c8f38-0aad-44c7-9064-478429495727", GetType(Crashes))

        Try
            ' Add your code here
            _cachedUiCtrApp = app

            Dim ribbonPanel As RibbonPanel = CreateRibbonPanel()

            ' Return Success
            Return Result.Succeeded

        Catch ex As Exception
            'Crashes.TrackError(ex)

            Return Result.Failed

        End Try
    End Function


    ''' <summary>
    ''' This method is called when Revit is about to exit.
    ''' All documents are closed before this method is called.
    ''' </summary>
    ''' <param name="app">An object passed to the external 
    ''' application which contains the controlled application.</param>
    ''' <returns>Return the status of the external application. 
    ''' A result of Succeeded means that the external application successfully shutdown. 
    ''' Cancelled can be used to signify that the user cancelled the external operation 
    ''' at some point. If false is returned then the Revit user should be warned of the 
    ''' failure of the external application to shut down correctly.</returns>
    Public Function OnShutdown(
      ByVal app As UIControlledApplication) _
    As Result Implements IExternalApplication.OnShutdown

        'TODO: Add shutdown code here

        'Must return some code
        Return Result.Succeeded
    End Function

    Function CreateRibbonPanel() As RibbonPanel
        Dim panel As RibbonPanel

        'Check if "Archisoft Tools already exists and use if its there
        Try
            panel = _cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString())
            panel.Name = "ARBG_TopoAlign_ExtApp"
            panel.Title = "Topo Align"
        Catch
            Dim ArchisoftPanel As Boolean = False
            If My.Computer.FileSystem.DirectoryExists("C:\ProgramData\Autodesk\ApplicationPlugins") = True Then
                For Each folder In My.Computer.FileSystem.GetDirectories("C:\ProgramData\Autodesk\ApplicationPlugins")
                    If folder.ToLower.Contains("archisoft") = True And folder.ToLower.Contains("archisoft topoalign") = False Then
                        ArchisoftPanel = True
                        Exit For
                    End If
                Next
            End If

            If ArchisoftPanel = True Then
                _cachedUiCtrApp.CreateRibbonTab("Archisoft Tools")
                panel = _cachedUiCtrApp.CreateRibbonPanel("Archisoft Tools", Guid.NewGuid().ToString())
                panel.Name = "ARBG_TopoAlign_ExtApp"
                panel.Title = "Topo Align"
            Else
                panel = _cachedUiCtrApp.CreateRibbonPanel("Topo Align")
            End If
        End Try


        Dim pbDataTopoAlign As PushButtonData = New PushButtonData("Align to Element", "Align to Element", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdAlignTopo")
        Dim pbTopoAlign As PushButton = CType(panel.AddItem(pbDataTopoAlign), PushButton)
        pbTopoAlign.ToolTip = "Adjust topo to edge or floor geometry"
        pbTopoAlign.LargeImage = RetriveImage("ARCHISOFT_topoalign.TopoAlign32.png")
        'pbTopoAlign.Image = RetriveImage("ARCHISOFT_topoalign.TopoAlign16x16.bmp")

        Dim pbDataPointsFromLines As PushButtonData = New PushButtonData("Points from Lines", "Points from Lines", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdPointsOnSurface")
        Dim pbPointsFromLines As PushButton = CType(panel.AddItem(pbDataPointsFromLines), PushButton)
        pbPointsFromLines.ToolTip = "Add points on surface along selected model lines. Model lines must be lines and arcs and be BELOW the topo surface."
        pbPointsFromLines.LargeImage = RetriveImage("ARCHISOFT_topoalign.PointsFromLines32.png")
        'pbPointsFromLines.Image = RetriveImage("ARCHISOFT_topoalign.TopoPoints16x16.bmp")

        Dim pbDataPointsAlongContours As PushButtonData = New PushButtonData("Points along contours", "Points along contours", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdPointsAlongContours")
        Dim pbPointsAlongContours As PushButton = CType(panel.AddItem(pbDataPointsAlongContours), PushButton)
        pbPointsAlongContours.ToolTip = "Add points on surface along selected contour model lines"
        pbPointsAlongContours.LargeImage = RetriveImage("ARCHISOFT_topoalign.PointsFromContours32.png")
        'pbPointsAlongContours.Image = RetriveImage("ARCHISOFT_topoalign.TopoPoints16x16.bmp")

        Dim pbDataResetRegion As PushButtonData = New PushButtonData("Reset region", "Reset region", Assembly.GetExecutingAssembly().Location, "ARCHISOFT_topoalign.cmdResetTopoRegion")
        Dim pbResetRegion As PushButton = CType(panel.AddItem(pbDataResetRegion), PushButton)
        pbResetRegion.ToolTip = "Copy points from existing topo surface to new topo surface within a region to undo changes made."
        pbResetRegion.LargeImage = RetriveImage("ARCHISOFT_topoalign.Reset32.png")
        'pbResetRegion.Image = RetriveImage("ARCHISOFT_topoalign.TopoReset16x16.bmp")

        'Help document
        Dim contextHelp As ContextualHelp
#If CONFIG = "2015" Then
        contextHelp = New ContextualHelp(ContextualHelpType.Url, "C:\ProgramData\Autodesk\ApplicationPlugins\Archisoft TopoAlign.bundle\Contents\TopoAlign.htm")
#ElseIf CONFIG = "2019" Then
        contextHelp = New ContextualHelp(ContextualHelpType.Url, "https://apps.autodesk.com/RVT/en/Detail/HelpDoc?appId=4811020092910907691&appLang=en&os=Win64")
#ElseIf CONFIG = "2020" Then
        contextHelp = New ContextualHelp(ContextualHelpType.Url, "https://apps.autodesk.com/RVT/en/Detail/HelpDoc?appId=7668296925673994353&appLang=en&os=Win64")
#ElseIf CONFIG = "2021" Then
        contextHelp = New ContextualHelp(ContextualHelpType.Url, "https://apps.autodesk.com/RVT/en/Detail/HelpDoc?appId=3561777884450830300&appLang=en&os=Win64")
#ElseIf CONFIG = "2022" Then
        contextHelp = New ContextualHelp(ContextualHelpType.Url, "https://apps.autodesk.com/ACD/en/Detail/HelpDoc?appId=7412914718855875408&appLang=en&os=Win64")
#End If

        pbTopoAlign.SetContextualHelp(contextHelp)

    End Function

    Private Shared Function RetriveImage(imagePath As String) As ImageSource
        Dim manifestResourceStream As Stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(imagePath)
        Select Case imagePath.Substring(imagePath.Length - 3)
            Case "jpg"
                Return DirectCast(New JpegBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.[Default]).Frames(0), ImageSource)
            Case "bmp"
                Return DirectCast(New BmpBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.[Default]).Frames(0), ImageSource)
            Case "png"
                Return DirectCast(New PngBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.[Default]).Frames(0), ImageSource)
            Case "ico"
                Return DirectCast(New IconBitmapDecoder(manifestResourceStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.[Default]).Frames(0), ImageSource)
            Case Else
                Return DirectCast(Nothing, ImageSource)
        End Select
    End Function
End Class
