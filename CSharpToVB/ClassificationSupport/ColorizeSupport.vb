﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Text

Imports CSharpToVBApp

Imports CSharpToVBConverter
Imports CSharpToVBConverter.ConversionResult

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.Emit

Public Module ColorizeSupport

    Private Function FilterOutTopLevelStatementCode(TextToCompile As String) As String
        If Not My.Settings.IncludeTopLevelStmtProtoInCode AndAlso TextToCompile.Contains("Top Level Code boilerplate is included,") Then
            Dim filteredCode As New StringBuilder
            Dim skipNext As Boolean
            Dim skipBlank As Boolean
            For Each e As IndexClass(Of String) In TextToCompile.SplitLines.WithIndex
                Dim stmt As String = e.Value
                If String.IsNullOrEmpty(stmt) Then
                    If Not skipBlank Then
                        filteredCode.AppendLine()
                        skipBlank = True
                    End If
                    Continue For
                End If
                skipBlank = False
                Select Case True
                    Case stmt.Trim.StartsWith("' Top Level Code boilerplate is included")
                        skipBlank = True
                    Case stmt.Trim.StartsWith("Namespace Application")
                    Case stmt.Trim.StartsWith("NotInheritable Class Program")
                    Case stmt.Trim.StartsWith("Private Shared ")
                    Case stmt.Trim.StartsWith("Public Shared ")
                        skipBlank = True
                        skipNext = True
                    Case stmt.Trim.StartsWith("End Sub")
                    Case stmt.Trim.StartsWith("End Class")
                    Case stmt.Trim.StartsWith("End Namespace")
                    Case stmt.StartsWith("            ")
                        filteredCode.AppendLine(stmt.Substring(12))
                    Case skipNext
                        skipNext = False
                    Case Else
                        filteredCode.AppendLine(stmt)
                End Select
            Next
            TextToCompile = filteredCode.ToString
        End If

        Return TextToCompile
    End Function

    Friend Sub Colorize(MainForm As Form1, FragmentRange As IEnumerable(Of Range), ConversionBuffer As RichTextBox)
        Dim currentChar As Integer = ConversionBuffer.SelectionStart
        Dim currentlength As Integer = ConversionBuffer.SelectionLength
        With ConversionBuffer
            For Each range As Range In FragmentRange
                If currentChar < range.TextSpan.Start Then
                    Continue For
                End If
                .Select(range.TextSpan.Start, range.TextSpan.Length)
                .SelectionColor = GetColorFromName(range.ClassificationType).Foreground
                Exit For
                Application.DoEvents()
            Next range
            .Select(currentChar, currentlength)
        End With
    End Sub

    Friend Sub Colorize(MainForm As Form1, FragmentRange As IEnumerable(Of Range), ConversionBuffer As RichTextBox, Lines As Integer, Optional failures As IEnumerable(Of Diagnostic) = Nothing)
        If MainForm._inColorize Then
            Exit Sub
        End If
        Try ' Prevent crash when exiting
            MainForm._inColorize = True
            If ConversionBuffer.Visible Then
                ConversionBuffer.Visible = False
            End If
            If failures Is Nothing Then
                MainForm.ListBoxErrorList.Enabled = False
            Else
                MainForm.ListBoxErrorList.Enabled = True
                For Each dia As Diagnostic In failures
                    MainForm.ListBoxErrorList.Items.Add($"{dia.Id} Line = {dia.Location.GetLineSpan.StartLinePosition.Line + 1} {dia.GetMessage}")
                Next
            End If

            MainForm.StatusStripConversionProgressBar.Maximum = Lines

            With ConversionBuffer
                .Clear()
                .BackColor = GetColorFromName(ThemeDefaultColor).Background
                .Select(.TextLength, 0)
                For Each range As Range In FragmentRange
                    .Select(.TextLength, 0)
                    .SelectionColor = GetColorFromName(range.ClassificationType).Foreground
                    .AppendText(range.Text)
                    If range.Text.Contains(vbLf, StringComparison.OrdinalIgnoreCase) Then
                        MainForm.StatusStripConversionProgressBar.Increment(range.Text.Count(CType(vbLf, Char)))
                        Application.DoEvents()
                    End If
                    If MainForm._requestToConvert?.CancelToken.IsCancellationRequested Then
                        Exit Sub
                    End If
                    Application.DoEvents()
                Next range
                If failures?.Count > 0 Then
                    For Each dia As Diagnostic In failures
                        Dim errorLine As Integer = dia.Location.GetLineSpan.StartLinePosition.Line
                        Dim errorCharactorPosition As Integer = dia.Location.GetLineSpan.StartLinePosition.Character
                        Dim length As Integer = dia.Location.GetLineSpan.EndLinePosition.Character - errorCharactorPosition
                        .Select(.GetFirstCharIndexFromLine(errorLine) + errorCharactorPosition, length)
                        Dim selectionColor As ColorDescriptor = GetColorFromName("Error")
                        .SelectionBackColor = selectionColor.Background
                        .SelectionColor = selectionColor.Foreground
                        .Select(.TextLength, 0)
                    Next
                    .Select(.GetFirstCharIndexFromLine(failures(0).Location.GetLineSpan.StartLinePosition.Line), 0)
                    .ScrollToCaret()
                End If
            End With
            If failures?.Count > 0 Then
                MainForm.LineNumbersForConversionInput.Visible = True
                MainForm.LineNumbersForConversionOutput.Visible = True
            End If
            MainForm.StatusStripConversionProgressBar.Clear()
        Catch ex As OperationCanceledException
        Catch ex As Exception
            Stop
        Finally
            ConversionBuffer.Visible = True
            ConversionBuffer.Refresh()
            Application.DoEvents()
            MainForm._inColorize = False
        End Try
    End Sub

    Friend Sub Compile_Colorize(MainForm As Form1, TextToCompile As String, VBPreprocessorSymbols As List(Of KeyValuePair(Of String, Object)))
        Dim compileResult As (Success As Boolean, EmitResult As EmitResult) = CompileVisualBasicString(TextToCompile, VBPreprocessorSymbols, DiagnosticSeverity.Error, MainForm._resultOfConversion)

        MainForm.LabelErrorCount.Text = $"Number Of Errors:  {MainForm._resultOfConversion.GetFilteredListOfFailures().Count}"
        If Not My.Settings.IncludeTopLevelStmtProtoInCode Then
            MainForm._inColorize = True

            MainForm._inColorize = False
        End If

        If compileResult.Success AndAlso compileResult.EmitResult.Success Then
            TextToCompile = FilterOutTopLevelStatementCode(TextToCompile)
            If My.Settings.ColorizeOutput Then
                Colorize(MainForm, GetClassifiedRanges(TextToCompile, LanguageNames.VisualBasic), MainForm.ConversionOutput, TextToCompile.SplitLines.Length)
                MainForm.ConversionOutput.Select(0, 0)
            Else
                MainForm.ConversionOutput.Text = TextToCompile
            End If
        Else
            If Not MainForm._resultOfConversion.GetFilteredListOfFailures().Any Then
                TextToCompile = FilterOutTopLevelStatementCode(TextToCompile)
                MainForm._resultOfConversion.ResultStatus = ResultTriState.Success
                If My.Settings.ColorizeOutput Then
                    Colorize(MainForm, GetClassifiedRanges(TextToCompile, LanguageNames.VisualBasic), MainForm.ConversionOutput, TextToCompile.SplitLines.Length, MainForm._resultOfConversion.GetFilteredListOfFailures())
                    MainForm.ConversionOutput.Select(0, 0)
                Else
                    MainForm.ConversionOutput.Text = TextToCompile
                End If
            Else
                Colorize(MainForm, GetClassifiedRanges(TextToCompile, LanguageNames.VisualBasic), MainForm.ConversionOutput, TextToCompile.SplitLines.Length, MainForm._resultOfConversion.GetFilteredListOfFailures())
            End If
        End If
        MainForm.ConversionOutput.Visible = True
        Application.DoEvents()
    End Sub

End Module
