﻿' Copyright (C) 2023  Andy https://github.com/AAndyProgram
' This program is free software: you can redistribute it and/or modify
' it under the terms of the GNU General Public License as published by
' the Free Software Foundation, either version 3 of the License, or
' (at your option) any later version.
'
' This program is distributed in the hope that it will be useful,
' but WITHOUT ANY WARRANTY
Imports System.Threading
Imports SCrawler.API.Base
Imports SCrawler.DownloadObjects.Groups
Imports PersonalUtilities.Functions.XML
Imports PersonalUtilities.Functions.XML.Base
Imports PersonalUtilities.Tools
Imports PersonalUtilities.Tools.Notifications
Namespace DownloadObjects
    Friend Class AutoDownloader : Inherits GroupParameters : Implements IIndexable, IEContainerProvider
        Friend Event PauseChanged(ByVal Value As PauseModes)
        Friend Enum Modes As Integer
            None = 0
            [Default] = 1
            All = 2
            Specified = 3
            Groups = 4
        End Enum
        Friend Enum PauseModes As Integer
            Disabled = -2
            Enabled = -1
            Unlimited = -1
            Until = 0
            H1 = 1
            H2 = 2
            H3 = 3
            H4 = 4
            H6 = 6
            H12 = 12
        End Enum
        Friend Const DefaultTimer As Integer = 60
#Region "Notifications"
        Private Const KeyOpenFolder As String = "_____OPEN_FOLDER_SCRAWLER_AUTOMATION"
        Private Const KeyOpenSite As String = "_____OPEN_SITE_SCRAWLER_AUTOMATION"
        Private Const KeyBttDismiss As String = "_____DISMISS_SCRAWLER_AUTOMATION"
        Private Const KeyBttPhoto As String = "_____PHOTO_SCRAWLER_AUTOMATION"
        Private ReadOnly UserKeys As List(Of NotifiedUser)
        Private Class NotifiedUser : Implements IDisposable
            Private ReadOnly Property User As IUserData
            Friend ReadOnly Property IUserDataKey As String
            Private ReadOnly Property Key As String
            Private ReadOnly Property KeyFolder As String
            Private ReadOnly Property KeySite As String
            Private ReadOnly Property KeyDismiss As String
            Private ReadOnly Property Images As Dictionary(Of String, SFile)
            Private ReadOnly Property AutoDownloaderSource As AutoDownloader
            Private Sub New()
                Images = New Dictionary(Of String, SFile)
            End Sub
            Private Sub New(ByVal _Key As String)
                Me.New
                Key = _Key
                KeyFolder = $"{Key}{KeyOpenFolder}"
                KeySite = $"{Key}{KeyOpenSite}"
                KeyDismiss = $"{Key}{KeyBttDismiss}"
            End Sub
            Friend Sub New(ByVal _Key As String, ByRef _User As IUserData, ByRef Source As AutoDownloader)
                Me.New(_Key)
                User = _User
                IUserDataKey = _User.Key
                AutoDownloaderSource = Source
                If _User.IncludedInCollection Then
                    Dim cn$ = _User.CollectionName
                    Dim i% = Settings.Users.FindIndex(Function(u) u.IsCollection And u.Name = cn)
                    If i >= 0 Then IUserDataKey = Settings.Users(i).Key
                End If
            End Sub
            Public Shared Widening Operator CType(ByVal Key As String) As NotifiedUser
                Return New NotifiedUser(Key)
            End Operator
            Friend Sub ShowNotification()
                Try
                    If Not AutoDownloaderSource Is Nothing And Settings.ProcessNotification(SettingsCLS.NotificationObjects.AutoDownloader) Then
                        If AutoDownloaderSource.ShowNotifications Then
                            If Not User Is Nothing Then
                                Dim Text$ = $"{IIf(User.IsSubscription, "[Subscription] ", String.Empty)}{User.Site} - {User.Name}{vbNewLine}" &
                                            $"Downloaded: {User.DownloadedPictures(False)} images, {User.DownloadedVideos(False)} videos"
                                Dim Title$
                                If Not User.CollectionName.IsEmptyString Then
                                    Title = User.CollectionName
                                Else
                                    Title = User.ToString
                                End If
                                Using Notify As New Notification(Text, Title) With {.Key = Key}
                                    Dim uPic As SFile = Nothing
                                    Dim uif As SFile = Nothing
                                    Dim uif_orig As SFile = Nothing
                                    Dim uif_compressed As SFile = Nothing
                                    Dim uifKey$ = String.Empty
                                    If AutoDownloaderSource.ShowPictureUser Then uPic = DirectCast(User, UserDataBase).GetUserPictureToastAddress
                                    If AutoDownloaderSource.ShowPictureUser AndAlso uPic.Exists Then Notify.Images = {New ToastImage(uPic)}
                                    If AutoDownloaderSource.ShowPictureDownloaded And User.DownloadedPictures(False) > 0 And Not User.IsSubscription Then
                                        uif = DirectCast(User, UserDataBase).GetLastImageAddress
                                        uif_orig = uif
                                        If uif.Exists Then
                                            uif_compressed = uif
                                            uif_compressed.Path = Settings.Cache.RootDirectory.Path
                                            uif_compressed.Name = $"360_{uif.Name}"
                                            Using imgR As New ImageRenderer(uif, EDP.SendToLog)
                                                Try : imgR.FitToWidth(360).Save(uif_compressed) : Catch : End Try
                                            End Using
                                            If uif_compressed.Exists Then uif = uif_compressed
                                            If uif.Exists Then
                                                Notify.Images = {New ToastImage(uif, IImage.Modes.Inline)}
                                                uifKey = $"{Key}_{Images.Keys.Count + 1}_{KeyBttPhoto}"
                                                If Not Images.ContainsKey(uifKey) Then Images.Add(uifKey, uif_orig)
                                            End If
                                        End If
                                    End If
                                    Notify.Buttons = {
                                        New ToastButton(KeyFolder, "Folder"),
                                        New ToastButton(KeySite, "Site")
                                    }
                                    If Not uifKey.IsEmptyString Then Notify.Buttons = {New ToastButton(uifKey, "Photo")}
                                    Notify.Buttons = {New ToastButton(KeyDismiss, "Dismiss")}
                                    Notify.Show()
                                End Using
                            End If
                        End If
                    End If
                Catch ex As Exception
                    ErrorsDescriber.Execute(EDP.SendToLog, ex, "[AutoDownloader.NotifiedUser.ShowNotification]")
                    If Not User Is Nothing Then
                        MainFrameObj.ShowNotification(SettingsCLS.NotificationObjects.AutoDownloader,
                                                      If(User.IsSubscription, "[Subscription] ", String.Empty) & User.ToString & vbNewLine &
                                                      $"Downloaded: {User.DownloadedPictures(False)} images, {User.DownloadedVideos(False)} videos" &
                                                      If(User.HasError, vbNewLine & "With errors", String.Empty))
                    End If
                End Try
            End Sub
            ''' <returns>True to activate</returns>
            Friend Function Open(ByVal _Key As String) As Boolean
                If Not User Is Nothing Then
                    If KeyDismiss = _Key Then
                    ElseIf Key = _Key Then
                        Return True
                    ElseIf KeyFolder = _Key Then
                        If User.IsSubscription Then
                            Return True
                        Else
                            User.OpenFolder()
                        End If
                    ElseIf KeySite = _Key Then
                        User.OpenSite()
                    ElseIf Images.ContainsKey(_Key) Then
                        Images(_Key).Open()
                    End If
                Else
                    Return True
                End If
                Return False
            End Function
            Public Overrides Function Equals(ByVal Obj As Object) As Boolean
                With CType(Obj, NotifiedUser)
                    Return .Key = Key Or .Key = KeyFolder Or .Key = KeySite Or .Key = KeyDismiss Or Images.ContainsKey(.Key)
                End With
            End Function
#Region "IDisposable Support"
            Private disposedValue As Boolean = False
            Protected Overridable Overloads Sub Dispose(ByVal disposing As Boolean)
                If Not disposedValue Then
                    If disposing Then Images.Clear()
                    disposedValue = True
                End If
            End Sub
            Protected Overrides Sub Finalize()
                Dispose(False)
                MyBase.Finalize()
            End Sub
            Friend Overloads Sub Dispose() Implements IDisposable.Dispose
                Dispose(True)
                GC.SuppressFinalize(Me)
            End Sub
#End Region
        End Class
#End Region
#Region "XML Names"
        Private Const Name_Mode As String = "Mode"
        Private Const Name_Groups As String = "Groups"
        Private Const Name_IsManual As String = "IsManual"
        Private Const Name_Timer As String = "Timer"
        Private Const Name_StartupDelay As String = "StartupDelay"
        Private Const Name_LastDownloadDate As String = "LastDownloadDate"
        Private Const Name_ShowNotifications As String = "Notify"
        Private Const Name_ShowPictureDown As String = "ShowDownloadedPicture"
        Private Const Name_ShowPictureUser As String = "ShowUserPicture"
        Private Const Name_ShowSimpleNotification As String = "ShowSimpleNotification"
#End Region
#Region "Declarations"
        Friend Property Source As Scheduler
        Private _Mode As Modes = Modes.None
        Friend Property Mode As Modes
            Get
                Return _Mode
            End Get
            Set(ByVal m As Modes)
                _Mode = m
                If _Mode = Modes.None Then [Stop]()
            End Set
        End Property
        Friend ReadOnly Property Groups As List(Of String)
        Friend Property IsManual As Boolean = False
        Friend Property Timer As Integer = DefaultTimer
        Friend Property StartupDelay As Integer = 1
        Friend Property ShowNotifications As Boolean = True
        Friend Property ShowPictureDownloaded As Boolean = True
        Friend Property ShowPictureUser As Boolean = True
        Friend Property ShowSimpleNotification As Boolean = False
        Private Property Index As Integer = -1 Implements IIndexable.Index
        Private Function SetIndex(ByVal Obj As Object, ByVal Index As Integer) As Object Implements IIndexable.SetIndex
            DirectCast(Obj, AutoDownloader).Index = Index
            Return Obj
        End Function
#Region "Date"
        Private ReadOnly LastDownloadDateXML As Date? = Nothing
        Private _LastDownloadDate As Date = Now.AddYears(-1)
        Private _LastDownloadDateChanged As Boolean = False
        Private _LastDownloadDateSkip As Date? = Nothing
        Friend Property LastDownloadDate As Date
            Get
                Return _LastDownloadDate
            End Get
            Set(ByVal d As Date)
                _LastDownloadDate = d
                If Not Initialization Then _LastDownloadDateChanged = True
            End Set
        End Property
        Private ReadOnly Property NextExecutionDate As Date
            Get
                Dim lds As Date = If(_LastDownloadDateSkip, Date.MinValue)
                If _PauseValue.HasValue Then
                    Return {LastDownloadDate.AddMinutes(Timer), _StartTime.AddMinutes(StartupDelay), _PauseValue.Value, lds}.Max
                Else
                    Return {LastDownloadDate.AddMinutes(Timer), _StartTime.AddMinutes(StartupDelay), lds}.Max
                End If
            End Get
        End Property
        Private ReadOnly DateProvider As New ADateTime(ADateTime.Formats.BaseDateTime)
        Private Function GetLastDateString() As String
            If LastDownloadDateXML.HasValue Or _LastDownloadDateChanged Then
                Return LastDownloadDate.ToStringDate(DateProvider)
            Else
                Return "never"
            End If
        End Function
        Private Function GetNextDateString() As String
            Return NextExecutionDate.ToStringDate(DateProvider)
        End Function
#End Region
#Region "Information"
        Friend ReadOnly Property Information As String
            Get
                Return $"Last download date: {GetLastDateString()} ({GetWorkingState()})"
            End Get
        End Property
        Private Function GetWorkingState() As String
            Dim OutStr$
            If Working Then
                If StartupDelay > 0 And _StartTime.AddMinutes(StartupDelay) > Now Then
                    OutStr = $"delayed until {_StartTime.AddMinutes(StartupDelay).ToStringDate(ADateTime.Formats.BaseDateTime)}"
                ElseIf _StopRequested Then
                    OutStr = "stopping"
                Else
                    OutStr = "working"
                End If
                If IsPaused Then
                    Select Case Pause
                        Case PauseModes.Unlimited : OutStr &= ", paused"
                        Case PauseModes.Until : OutStr &= $", paused (until {AConvert(Of String)(_PauseValue, ADateTime.Formats.BaseDateTime, "?")})"
                        Case Else : OutStr &= $", paused ([{Pause}] until {AConvert(Of String)(_PauseValue, ADateTime.Formats.BaseDateTime, "?")})"
                    End Select
                End If
            Else
                OutStr = "stopped"
            End If
            Return OutStr
        End Function
        Public Overrides Function ToString() As String
            If IsManual Then
                Return $"{Name} (manual): last download date: {GetLastDateString()}"
            Else
                Return $"{Name} ({GetWorkingState()}): last download date: {GetLastDateString()}; next run: {GetNextDateString()}"
            End If
        End Function
#End Region
#End Region
#Region "Initializer"
        Private ReadOnly Initialization As Boolean = True
        Private _IsNewPlan As Boolean = False
        Friend ReadOnly Property IsNewPlan As Boolean
            Get
                Return _IsNewPlan
            End Get
        End Property
        Friend Sub New(Optional ByVal IsNewPlan As Boolean = False)
            Groups = New List(Of String)
            UserKeys = New List(Of NotifiedUser)
            _IsNewPlan = IsNewPlan
            Initialization = False
        End Sub
        Friend Sub New(ByVal x As EContainer)
            Me.New
            Initialization = True
            Mode = x.Value(Name_Mode).FromXML(Of Integer)(Modes.None)
            Import(x)
            If Name.IsEmptyString Then Name = "Default"
            Groups.ListAddList(x.Value(Name_Groups).StringToList(Of String)("|"), LAP.NotContainsOnly)

            IsManual = x.Value(Name_IsManual).FromXML(Of Boolean)(False)
            Timer = x.Value(Name_Timer).FromXML(Of Integer)(DefaultTimer)
            If Timer <= 0 Then Timer = DefaultTimer
            StartupDelay = x.Value(Name_StartupDelay).FromXML(Of Integer)(0)
            If StartupDelay < 0 Then StartupDelay = 0
            ShowNotifications = x.Value(Name_ShowNotifications).FromXML(Of Boolean)(True)
            ShowPictureDownloaded = x.Value(Name_ShowPictureDown).FromXML(Of Boolean)(True)
            ShowPictureUser = x.Value(Name_ShowPictureUser).FromXML(Of Boolean)(True)
            ShowSimpleNotification = x.Value(Name_ShowSimpleNotification).FromXML(Of Boolean)(False)
            LastDownloadDateXML = AConvert(Of Date)(x.Value(Name_LastDownloadDate), DateProvider, Nothing)
            If LastDownloadDateXML.HasValue Then
                LastDownloadDate = LastDownloadDateXML.Value
            Else
                LastDownloadDate = Now.AddYears(-1)
            End If
            Initialization = False
        End Sub
#End Region
#Region "ICopier Support"
        Friend Overrides Function Copy() As Object
            Dim newObj As New AutoDownloader(True)
            newObj.Copy(Me)
            With newObj
                .Name = String.Empty
                ._Mode = _Mode
                .Groups.ListAddList(Groups, LAP.ClearBeforeAdd)
                .IsManual = IsManual
                .Timer = Timer
                .StartupDelay = StartupDelay
                .ShowNotifications = ShowNotifications
                .ShowPictureDownloaded = ShowPictureDownloaded
                .ShowPictureUser = ShowPictureUser
                .ShowSimpleNotification = ShowSimpleNotification
            End With
            Return newObj
        End Function
#End Region
#Region "Groups Support"
        Friend Sub GROUPS_Updated(ByVal Sender As DownloadGroup)
            If Groups.Count > 0 Then
                Dim i% = Groups.IndexOf(Sender.NameBefore)
                If i >= 0 Then Groups(i) = Sender.Name : Update()
            End If
        End Sub
        Friend Sub GROUPS_Deleted(ByVal Sender As DownloadGroup)
            If Groups.Count > 0 Then
                Dim i% = Groups.IndexOf(Sender.Name)
                If i >= 0 Then Groups.RemoveAt(i) : Update()
            End If
        End Sub
#End Region
#Region "Update"
        Friend Sub Update()
            If Not Source Is Nothing Then Source.Update()
        End Sub
        Private Function ToEContainer(Optional ByVal e As ErrorsDescriber = Nothing) As EContainer Implements IEContainerProvider.ToEContainer
            Return Export(New EContainer(Scheduler.Name_Plan, String.Empty) From {
                                         New EContainer(Name_Mode, CInt(Mode)),
                                         New EContainer(Name_Groups, Groups.ListToString("|")),
                                         New EContainer(Name_IsManual, IsManual.BoolToInteger),
                                         New EContainer(Name_Timer, Timer),
                                         New EContainer(Name_StartupDelay, StartupDelay),
                                         New EContainer(Name_ShowNotifications, ShowNotifications.BoolToInteger),
                                         New EContainer(Name_ShowPictureDown, ShowPictureDownloaded.BoolToInteger),
                                         New EContainer(Name_ShowPictureUser, ShowPictureUser.BoolToInteger),
                                         New EContainer(Name_ShowSimpleNotification, ShowSimpleNotification.BoolToInteger),
                                         New EContainer(Name_LastDownloadDate, CStr(AConvert(Of String)(If(LastDownloadDateXML.HasValue Or _LastDownloadDateChanged,
                                                                                                           CObj(LastDownloadDate), Nothing), DateProvider, String.Empty)))
                          })
        End Function
#End Region
#Region "Execution"
        Private AThread As Thread
        Friend ReadOnly Property Working As Boolean
            Get
                Return If(AThread?.IsAlive, False)
            End Get
        End Property
        Private _StartTime As Date = Now
        Friend Sub Start(ByVal Init As Boolean, Optional ByVal Force As Boolean = False)
            If Not IsManual Or Force Then
                If Init Then _StartTime = Now
                _IsNewPlan = False
                If Not Working And Not Mode = Modes.None Then
                    AThread = New Thread(New ThreadStart(AddressOf Checker))
                    AThread.SetApartmentState(ApartmentState.MTA)
                    AThread.Start()
                End If
            End If
        End Sub
        Private _StopRequested As Boolean = False
        Private _Pause As PauseModes = PauseModes.Disabled
        Private _PauseValue As Date? = Nothing
        Friend Property Pause(Optional ByVal DateLimit As Date? = Nothing) As PauseModes
            Get
                Return _Pause
            End Get
            Set(ByVal p As PauseModes)
                _Pause = p
                Select Case p
                    Case PauseModes.Disabled, PauseModes.Unlimited : _PauseValue = Nothing
                    Case PauseModes.H1 : _PauseValue = Now.AddHours(1)
                    Case PauseModes.H2 : _PauseValue = Now.AddHours(2)
                    Case PauseModes.H3 : _PauseValue = Now.AddHours(3)
                    Case PauseModes.H4 : _PauseValue = Now.AddHours(4)
                    Case PauseModes.H6 : _PauseValue = Now.AddHours(6)
                    Case PauseModes.H12 : _PauseValue = Now.AddHours(12)
                    Case PauseModes.Until : _PauseValue = DateLimit
                    Case Else : _PauseValue = Nothing
                End Select
                RaiseEvent PauseChanged(p)
            End Set
        End Property
        Private ReadOnly Property IsPaused As Boolean
            Get
                If Not _Pause = PauseModes.Disabled Then
                    If _PauseValue.HasValue Then
                        If _PauseValue.Value > Now Then
                            Return True
                        Else
                            _Pause = PauseModes.Disabled
                            _PauseValue = Nothing
                            RaiseEvent PauseChanged(_Pause)
                            Return False
                        End If
                    Else
                        Return True
                    End If
                Else
                    Return False
                End If
            End Get
        End Property
        Friend Sub [Stop]()
            If Working Then _StopRequested = True
        End Sub
        Friend Overloads Sub Skip()
            If LastDownloadDate.AddMinutes(Timer) <= Now Then
                _LastDownloadDateSkip = Now.AddMinutes(Timer)
            Else
                _LastDownloadDateSkip = LastDownloadDate.AddMinutes(Timer)
            End If
        End Sub
        Friend Overloads Sub Skip(ByVal Minutes As Integer)
            _LastDownloadDateSkip = If(_LastDownloadDateSkip, Now).AddMinutes(Minutes)
        End Sub
        Friend Overloads Sub Skip(ByVal ToDate As Date)
            _LastDownloadDateSkip = ToDate
        End Sub
        Friend Sub SkipReset()
            _LastDownloadDateSkip = Nothing
        End Sub
        Friend Sub ForceStart()
            _ForceStartRequested = True
            If IsManual Then Start(False, True)
        End Sub
        Private _ForceStartRequested As Boolean = False
        Private _SpecialDelayUse As Boolean = False
        Private _SpecialDelayTime As Date? = Nothing
        Private Sub Checker()
            Try
                Dim _StartDownload As Boolean
                While (Not _StopRequested Or Downloader.Working) And Not Mode = Modes.None
                    If ((IsManual And _ForceStartRequested) Or (NextExecutionDate < Now And Not IsPaused) Or _ForceStartRequested) And
                       Not _StopRequested And Not Mode = Modes.None Then
                        If Downloader.Working Then
                            _SpecialDelayUse = True
                        Else
                            If _SpecialDelayUse And Not _SpecialDelayTime.HasValue Then _SpecialDelayTime = Now.AddSeconds(10)
                            If Not _SpecialDelayUse OrElse (_SpecialDelayTime.HasValue AndAlso _SpecialDelayTime.Value < Now) Then
                                _SpecialDelayUse = False
                                _SpecialDelayTime = Nothing
                                _StartDownload = False
                                If Settings.Automation.Count = 1 Or _ForceStartRequested Or Index = -1 Then
                                    _StartDownload = True
                                Else
                                    _StartDownload = NextExecutionDate.AddMilliseconds(1000 * (Index + 1)).Ticks <= Now.Ticks
                                End If
                                If _StartDownload Then
                                    Download()
                                    If IsManual Then Exit While
                                End If
                            End If
                        End If
                    End If
                    Thread.Sleep(500)
                End While
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, "[AutoDownloader.Checker]")
            Finally
                _StopRequested = False
            End Try
        End Sub
        Private _Downloading As Boolean = False
        Friend ReadOnly Property Downloading As Boolean
            Get
                Return _Downloading
            End Get
        End Property
        Private Sub Download()
            _Downloading = True
            Dim Keys As New List(Of String)
            Try
                Dim users As New List(Of IUserData)
                Dim GName$
                Dim i%
                Dim DownloadedUsersCount% = 0
                Dim DownloadedSubscriptionsCount% = 0
                Dim simple As Boolean = ShowSimpleNotification And ShowNotifications
                Dim notify As Action = Sub()
                                           Try
                                               With Downloader.Downloaded
                                                   If ShowNotifications And .Count > 0 Then
                                                       For indx% = 0 To .Count - 1
                                                           With .Item(indx)
                                                               If Keys.Contains(.Key) Then
                                                                   If simple Then
                                                                       If .IsSubscription Then
                                                                           DownloadedSubscriptionsCount += 1
                                                                       Else
                                                                           DownloadedUsersCount += 1
                                                                       End If
                                                                   Else
                                                                       ShowNotification(.Self)
                                                                   End If
                                                                   Keys.Remove(.Key)
                                                               End If
                                                           End With
                                                       Next
                                                   End If
                                               End With
                                           Catch n_ex As Exception
                                           End Try
                                       End Sub
                Select Case Mode
                    Case Modes.All
                        Dim CheckLabels As Predicate(Of IUserData) = Function(ByVal u As IUserData) As Boolean
                                                                         If LabelsExcluded.Count = 0 Then
                                                                             Return True
                                                                         ElseIf u.Labels.Count = 0 Then
                                                                             Return True
                                                                         Else
                                                                             Return Not u.Labels.ListContains(LabelsExcluded)
                                                                         End If
                                                                     End Function
                        Dim CheckSites As Predicate(Of IUserData) = Function(u) SitesExcluded.Count = 0 OrElse Not SitesExcluded.Contains(u.Site)
                        Dim ExistsPredicate As Predicate(Of IUserData)
                        If Subscriptions Then
                            If SubscriptionsOnly Then
                                ExistsPredicate = UserExistsSubscriptionsPredicate
                            Else
                                ExistsPredicate = UserExistsPredicate
                            End If
                        Else
                            ExistsPredicate = UserExistsNonSubscriptionsPredicate
                        End If
                        users.ListAddList(Settings.GetUsers(Function(u) ExistsPredicate(u) And CheckLabels.Invoke(u) And CheckSites.Invoke(u)))
                        If UsersCount <> 0 And users.Count > 0 Then
                            users = users.ListTake(If(UsersCount > 0, -1, -2), Math.Abs(UsersCount))
                            If UsersCount < 0 Then users = users.ListReverse
                        End If
                    Case Modes.Default
                        Using g As New GroupParameters
                            g.LabelsExcluded.ListAddList(LabelsExcluded)
                            g.SitesExcluded.ListAddList(SitesExcluded)
                            users.ListAddList(DownloadGroup.GetUsers(g))
                        End Using
                    Case Modes.Specified : users.ListAddList(DownloadGroup.GetUsers(Me))
                    Case Modes.Groups
                        If Groups.Count > 0 And Settings.Groups.Count > 0 Then
                            For Each GName In Groups
                                i = Settings.Groups.IndexOf(GName)
                                If i >= 0 Then users.ListAddList(Settings.Groups(i).GetUsers, LAP.IgnoreICopier, LAP.NotContainsOnly)
                            Next
                        End If
                End Select
                If users.Count > 0 Then
                    Keys.ListAddList(users.Select(Function(u) u.Key))
                    With Downloader
                        .AutoDownloaderWorking = True
                        If .Downloaded.Count > 0 Then .Downloaded.RemoveAll(Function(u) Keys.Contains(u.Key)) : .InvokeDownloadsChangeEvent()
                        .AddRange(users, True)
                        While .Working Or .Count > 0 : notify.Invoke() : Thread.Sleep(200) : End While
                        .AutoDownloaderWorking = False
                        notify.Invoke
                        If simple And DownloadedUsersCount + DownloadedSubscriptionsCount > 0 Then
                            Dim msg$ = String.Empty
                            If DownloadedUsersCount > 0 Then msg = $"{DownloadedUsersCount} user(s) "
                            If DownloadedSubscriptionsCount > 0 Then msg &= $"{IIf(DownloadedUsersCount > 0, "and ", String.Empty)}{DownloadedSubscriptionsCount} subscription(s) "
                            msg &= $"downloaded with scheduler plan '{Name}'"
                            MainFrameObj.ShowNotification(SettingsCLS.NotificationObjects.AutoDownloader, msg)
                        End If
                    End With
                End If
            Catch ex As Exception
                ErrorsDescriber.Execute(EDP.SendToLog, ex, "[AutoDownloader.Download]")
            Finally
                Keys.Clear()
                LastDownloadDate = Now
                Update()
                _Downloading = False
                _ForceStartRequested = False
                _LastDownloadDateSkip = Nothing
            End Try
        End Sub
        Private Sub ShowNotification(ByVal u As IUserData)
            Dim k$ = $"{Name}_{u.Key}"
            Dim i% = UserKeys.IndexOf(k)
            If i >= 0 Then
                UserKeys(i).ShowNotification()
            Else
                UserKeys.Add(New NotifiedUser(k, Settings.GetUser(u), Me))
                UserKeys.Last.ShowNotification()
            End If
        End Sub
        Friend Function NotificationClicked(ByVal Key As String, ByRef Found As Boolean, ByRef ActivateForm As Boolean) As Boolean
            Dim i% = UserKeys.IndexOf(Key)
            If i >= 0 Then
                Found = True
                ActivateForm = UserKeys(i).Open(Key)
                MainFrameObj.FocusUser(UserKeys(i).IUserDataKey, ActivateForm)
                Return True
            Else
                Return False
            End If
        End Function
#End Region
#Region "IDisposable Support"
        Protected Overrides Sub Dispose(ByVal disposing As Boolean)
            If Not disposedValue And disposing Then
                [Stop]()
                UserKeys.ListClearDispose()
                Groups.Clear()
            End If
            MyBase.Dispose(disposing)
        End Sub
#End Region
    End Class
End Namespace