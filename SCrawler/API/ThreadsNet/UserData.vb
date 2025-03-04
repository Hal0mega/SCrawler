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
Imports SCrawler.API.YouTube.Objects
Imports PersonalUtilities.Functions.XML
Imports PersonalUtilities.Functions.RegularExpressions
Imports PersonalUtilities.Tools.Web.Documents.JSON
Imports PersonalUtilities.Tools.Web.Clients
Imports PersonalUtilities.Tools.Web.Clients.EventArguments
Imports IGS = SCrawler.API.Instagram.SiteSettings
Namespace API.ThreadsNet
    Friend Class UserData : Inherits Instagram.UserData
#Region "Declarations"
        Private ReadOnly Property MySettings As SiteSettings
            Get
                Return HOST.Source
            End Get
        End Property
        Private ReadOnly DefaultParser_ElemNode_Default() As Object = {"node", "thread_items", 0, "post"}
        Private OPT_LSD As String = String.Empty
        Private OPT_FB_DTSG As String = String.Empty
        Private ReadOnly Property Valid As Boolean
            Get
                Return Not OPT_LSD.IsEmptyString And Not OPT_FB_DTSG.IsEmptyString And Not ID.IsEmptyString
            End Get
        End Property
#End Region
#Region "Loader"
        Protected Overrides Sub LoadUserInformation_OptionalFields(ByRef Container As XmlFile, ByVal Loading As Boolean)
        End Sub
#End Region
#Region "Exchange"
        Friend Overrides Function ExchangeOptionsGet() As Object
            Return Nothing
        End Function
        Friend Overrides Sub ExchangeOptionsSet(ByVal Obj As Object)
        End Sub
#End Region
#Region "Initializer"
        Friend Sub New()
            ObtainMedia_SetReelsFunc()
            ObtainMedia_AllowAbstract = True
            DefaultParser_ElemNode = DefaultParser_ElemNode_Default
            DefaultParser_PostUrlCreator = Function(post) $"https://www.threads.net/@{NameTrue}/post/{post.Code}"
        End Sub
#End Region
#Region "Download functions"
        Protected Overrides Sub DownloadDataF(ByVal Token As CancellationToken)
            Dim errorFound As Boolean = False
            Try
                Responser.Method = "POST"
                AddHandler Responser.ResponseReceived, AddressOf Responser_ResponseReceived
                LoadSavePostsKV(True)
                OPT_LSD = String.Empty
                OPT_FB_DTSG = String.Empty
                DownloadData(String.Empty, Token)
            Catch ex As Exception
                errorFound = True
                Throw ex
            Finally
                Responser.Method = "POST"
                UpdateResponser()
                MySettings.UpdateResponserData(Responser)
                If Not errorFound Then LoadSavePostsKV(False)
            End Try
        End Sub
        Protected Overrides Sub UpdateResponser()
            If Not Responser Is Nothing AndAlso Not Responser.Disposed Then
                RemoveHandler Responser.ResponseReceived, AddressOf Responser_ResponseReceived
            End If
        End Sub
        Protected Overrides Sub Responser_ResponseReceived(ByVal Sender As Object, ByVal e As WebDataResponse)
            If e.CookiesExists Then
                Dim csrf$ = If(e.Cookies.FirstOrDefault(Function(v) v.Name.StringToLower = IGS.Header_CSRF_TOKEN_COOKIE)?.Value, String.Empty)
                If Not csrf.IsEmptyString AndAlso Not AEquals(Of String)(csrf, Responser.Headers.Value(IGS.Header_CSRF_TOKEN)) Then _
                   Responser.Headers.Add(IGS.Header_CSRF_TOKEN, csrf)
            End If
        End Sub
        Private Overloads Sub DownloadData(ByVal Cursor As String, ByVal Token As CancellationToken)
            Const urlPattern$ = "https://www.threads.net/api/graphql?lsd={0}&variables={1}&doc_id=6371597506283707&fb_api_req_friendly_name=BarcelonaProfileThreadsTabRefetchableQuery&server_timestamps=true&fb_dtsg={2}"
            Const var_init$ = """userID"":""{0}"""
            Const var_cursor$ = """after"":""{1}"",""before"":null,""first"":25,""last"":null,""userID"":""{0}"",""__relay_internal__pv__BarcelonaIsLoggedInrelayprovider"":true,""__relay_internal__pv__BarcelonaIsFeedbackHubEnabledrelayprovider"":false"
            Dim URL$ = String.Empty
            Try
                If Not Valid Then
                    Dim idIsNull As Boolean = ID.IsEmptyString
                    UpdateCredentials()
                    If idIsNull And Not ID.IsEmptyString Then _ForceSaveUserInfo = True
                End If
                If Not Valid Then Throw New Plugin.ExitException("Some credentials are missing")

                Responser.Method = "POST"
                Responser.Referer = $"https://www.threads.net/@{NameTrue}"
                Responser.Headers.Add(Header_FB_LSD, OPT_LSD)

                Dim nextCursor$ = String.Empty
                Dim dataFound As Boolean = False

                Dim vars$
                If Cursor.IsEmptyString Then
                    vars = String.Format(var_init, ID)
                Else
                    vars = String.Format(var_cursor, ID, Cursor)
                End If
                vars = SymbolsConverter.ASCII.EncodeSymbolsOnly("{" & vars & "}")

                URL = String.Format(urlPattern, OPT_LSD, vars, SymbolsConverter.ASCII.EncodeSymbolsOnly(OPT_FB_DTSG))

                Using j As EContainer = GetDocument(URL, Token)
                    If j.ListExists Then
                        With j({"data", "mediaData"})
                            If .ListExists Then
                                nextCursor = .Value({"page_info"}, "end_cursor")
                                With .Item({"edges"})
                                    If .ListExists Then dataFound = DefaultParser(.Self, Sections.Timeline, Token)
                                End With
                            End If
                        End With
                    End If
                End Using

                If dataFound And Not nextCursor.IsEmptyString Then DownloadData(nextCursor, Token)
            Catch ex As Exception
                ProcessException(ex, Token, $"data downloading error [{URL}]")
            End Try
        End Sub
        Private Function GetDocument(ByVal URL As String, ByVal Token As CancellationToken, Optional ByVal Round As Integer = 0) As EContainer
            Try
                ThrowAny(Token)
                If Round > 0 AndAlso Not UpdateCredentials() Then Throw New Exception("Failed to update credentials")
                ThrowAny(Token)
                Dim r$ = Responser.GetResponse(URL)
                If Not r.IsEmptyString Then Return JsonDocument.Parse(r) Else Throw New Exception("Failed to get a response")
            Catch ex As Exception
                If Round = 0 Then
                    Return GetDocument(URL, Token, Round + 1)
                Else
                    Throw ex
                End If
            End Try
        End Function
        Private Function UpdateCredentials(Optional ByVal e As ErrorsDescriber = Nothing) As Boolean
            Dim URL$ = $"https://www.threads.net/@{NameTrue}"
            OPT_LSD = String.Empty
            OPT_FB_DTSG = String.Empty
            Try
                Responser.Method = "GET"
                Responser.Referer = URL
                Responser.Headers.Remove(Header_FB_LSD)
                Dim r$ = Responser.GetResponse(URL,, EDP.SendToLog + EDP.ThrowException)
                Dim rr As RParams
                Dim tt$, ttVal$
                If Not r.IsEmptyString Then
                    rr = RParams.DM(Instagram.PageTokenRegexPatternDefault, 0, RegexReturn.List, EDP.ReturnValue)
                    Dim tokens As List(Of String) = RegexReplace(r, rr)
                    If tokens.ListExists Then
                        With rr
                            .Match = Nothing
                            .MatchSub = 1
                            .WhatGet = RegexReturn.Value
                        End With
                        For Each tt In tokens
                            If Not OPT_FB_DTSG.IsEmptyString And Not OPT_LSD.IsEmptyString Then
                                Exit For
                            Else
                                ttVal = RegexReplace(tt, rr)
                                If Not ttVal.IsEmptyString Then
                                    If ttVal.Contains(":") Then
                                        If OPT_FB_DTSG.IsEmptyString Then OPT_FB_DTSG = ttVal
                                    Else
                                        If OPT_LSD.IsEmptyString Then OPT_LSD = ttVal
                                    End If
                                End If
                            End If
                        Next
                    End If
                    If ID.IsEmptyString Then ID = RegexReplace(r, RParams.DMS("""props"":\{""user_id"":""(\d+)""\},", 1, EDP.ReturnValue))
                End If
                Return Valid
            Catch ex As Exception
                Dim notFound$ = String.Empty
                If OPT_FB_DTSG.IsEmptyString Then notFound.StringAppend(Header_FB_LSD)
                If OPT_LSD.IsEmptyString Then notFound.StringAppend("lsd")
                If ID.IsEmptyString Then notFound.StringAppend("User ID")
                LogError(ex, $"failed to update some{IIf(notFound.IsEmptyString, String.Empty, $" ({notFound})")} credentials", e)
                Return False
            End Try
        End Function
#End Region
#Region "ReparseMissing"
        Protected Overrides Sub ReparseMissing(ByVal Token As CancellationToken)
            Const varsPattern$ = """postID"":""{0}"",""userID"":""{1}"",""__relay_internal__pv__BarcelonaIsLoggedInrelayprovider"":true,""__relay_internal__pv__BarcelonaIsFeedbackHubEnabledrelayprovider"":false"
            'Const varsPattern$ = "{""postID"":""{0}"",""__relay_internal__pv__BarcelonaIsLoggedInrelayprovider"":true,""__relay_internal__pv__BarcelonaIsFeedbackHubEnabledrelayprovider"":false}"
            Const urlPattern$ = "https://www.threads.net/api/graphql?lsd={0}&variables={1}&fb_api_req_friendly_name=BarcelonaPostPageQuery&server_timestamps=true&fb_dtsg={2}&doc_id=25460088156920903"
            Dim rList As New List(Of Integer)
            Dim URL$ = String.Empty
            DefaultParser_ElemNode = Nothing
            DefaultParser_IgnorePass = True
            Try
                If ContentMissingExists Then
                    Responser.Method = "POST"
                    Responser.Referer = $"https://www.threads.net/@{NameTrue}"
                    If Not IsSingleObjectDownload AndAlso Not UpdateCredentials() Then Throw New Exception("Failed to update credentials")
                    Dim m As UserMedia
                    Dim vars$
                    Dim j As EContainer
                    ProgressPre.ChangeMax(_ContentList.Count)
                    For i% = 0 To _ContentList.Count - 1
                        ProgressPre.Perform()
                        m = _ContentList(i)
                        If m.State = UserMedia.States.Missing And Not m.Post.ID.IsEmptyString Then
                            ThrowAny(Token)
                            vars = SymbolsConverter.ASCII.EncodeSymbolsOnly("{" & String.Format(varsPattern, m.Post.ID.Split("_").FirstOrDefault, ID) & "}")
                            URL = String.Format(urlPattern, OPT_LSD, vars, SymbolsConverter.ASCII.EncodeSymbolsOnly(OPT_FB_DTSG))

                            j = GetDocument(URL, Token)
                            If j.ListExists Then
                                With j.ItemF({"data", "data", "edges", 0, "node", "thread_items", 0, "post"})
                                    If .ListExists AndAlso DefaultParser({ .Self}, Sections.Timeline, Token) Then rList.Add(i)
                                End With
                                j.Dispose()
                            End If
                        End If
                    Next
                End If
            Catch ex As Exception
                ProcessException(ex, Token, $"ReparseMissing error [{URL}]")
            Finally
                DefaultParser_ElemNode = DefaultParser_ElemNode_Default
                DefaultParser_IgnorePass = False
                If rList.Count > 0 Then
                    For i% = rList.Count - 1 To 0 Step -1 : _ContentList.RemoveAt(rList(i)) : Next
                    rList.Clear()
                End If
            End Try
        End Sub
#End Region
#Region "DownloadSingleObject"
        Protected Overrides Sub DownloadSingleObject_GetPosts(ByVal Data As IYouTubeMediaContainer, ByVal Token As CancellationToken)
            Dim url$ = Data.URL_BASE.IfNullOrEmpty(Data.URL)
            Dim postCode$ = RegexReplace(url, RParams.DMS("post/([^/\?&]+)", 1, EDP.ReturnValue))
            If Not postCode.IsEmptyString Then
                Dim postId$ = CodeToID(postCode)
                If Not postId.IsEmptyString Then
                    _NameTrue = MySettings.IsMyUser(url).UserName
                    DefaultParser_PostUrlCreator = Function(post) url
                    If Not _NameTrue.IsEmptyString AndAlso UpdateCredentials(EDP.ReturnValue) Then
                        _ContentList.Add(New UserMedia(url) With {.State = UserMedia.States.Missing, .Post = postId})
                        ReparseMissing(Token)
                    End If
                End If
            End If
        End Sub
#End Region
#Region "ThrowAny"
        Friend Overrides Sub ThrowAny(ByVal Token As CancellationToken)
            ThrowAnyImpl(Token)
        End Sub
#End Region
#Region "DownloadingException"
        Protected Overrides Function DownloadingException(ByVal ex As Exception, ByVal Message As String, Optional ByVal FromPE As Boolean = False,
                                                          Optional ByVal EObj As Object = Nothing) As Integer
            Return 0
        End Function
#End Region
    End Class
End Namespace