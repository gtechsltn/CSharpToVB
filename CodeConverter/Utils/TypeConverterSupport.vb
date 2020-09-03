﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports System.Runtime.CompilerServices
Imports CSharpToVBCodeConverter.ToVisualBasic

Imports Microsoft.CodeAnalysis
Imports Microsoft.CodeAnalysis.CSharp

Imports CSS = Microsoft.CodeAnalysis.CSharp.Syntax
Imports VB = Microsoft.CodeAnalysis.VisualBasic
Imports Factory = Microsoft.CodeAnalysis.VisualBasic.SyntaxFactory
Imports VBS = Microsoft.CodeAnalysis.VisualBasic.Syntax

Namespace CSharpToVBCodeConverter

    Public Module TypeConverterSupport

        <Extension>
        Friend Function ConvertToType(PossibleTupleType As ITypeSymbol) As VBS.TypeSyntax
            If PossibleTupleType.IsKind(SymbolKind.ArrayType) Then
                Dim ElementType As VBS.TypeSyntax = DirectCast(PossibleTupleType, IArrayTypeSymbol).ElementType.ConvertToType()
                If TypeOf ElementType Is VBS.ArrayTypeSyntax Then
                    Return ElementType
                End If
                Return Factory.ArrayType(ElementType)
            End If
            If PossibleTupleType.IsTupleType Then
                Dim TupleElementList As New List(Of VBS.TupleElementSyntax)
                For Each TupleElement As IFieldSymbol In DirectCast(PossibleTupleType, INamedTypeSymbol).TupleElements
                    TupleElementList.Add(TupleElement.ConvertToTupleElement)
                Next
                Return Factory.TupleType(TupleElementList.ToArray)
            End If
            If PossibleTupleType.Name = "Tuple" Then
                Dim TupleElementList As New List(Of VBS.TypeSyntax)
                For Each TupleElement As ITypeSymbol In DirectCast(PossibleTupleType, INamedTypeSymbol).TypeArguments
                    TupleElementList.Add(TupleElement.ConvertToType())
                Next
                Return Factory.GenericName("Tuple", Factory.TypeArgumentList(Factory.SeparatedList(TupleElementList)))
            End If
            Dim PossibleName As String = PossibleTupleType.ToString.Trim
            Dim StartIndex As Integer = PossibleName.IndexOf("<", StringComparison.Ordinal)
            If StartIndex > 0 Then
                Dim IndexOfLastGreaterThan As Integer = PossibleName.LastIndexOf(">", StringComparison.Ordinal)
                Dim Name As String = PossibleName.Substring(0, StartIndex)
                Dim PossibleTypes As String = PossibleName.Substring(StartIndex + 1, IndexOfLastGreaterThan - StartIndex - 1)
                If PossibleTupleType.ToString.StartsWith("System.Func", StringComparison.Ordinal) Then
                    Dim DictionaryTypeElement As New List(Of VBS.TypeSyntax)
                    While PossibleTypes.Length > 0
                        Dim EndIndex As Integer
                        ' Tuple
                        If PossibleTypes.StartsWith("(", StringComparison.Ordinal) Then
                            ' Tuple
                            EndIndex = PossibleTypes.LastIndexOf(")", StringComparison.Ordinal)
                            DictionaryTypeElement.Add(PossibleTypes.Substring(0, EndIndex + 1).Trim.ConvertCSTupleToVBType)
                            EndIndex += 1
                        Else
                            Try
                                ' Type
                                Dim commaIndex As Integer = PossibleTypes.IndexOf(",", StringComparison.Ordinal)
                                Dim FirstLessThan As Integer = PossibleTypes.IndexOf("<", StringComparison.Ordinal)
                                If commaIndex = -1 Then
                                    EndIndex = PossibleTypes.Length
                                ElseIf FirstLessThan = -1 OrElse commaIndex < FirstLessThan Then
                                    EndIndex = commaIndex
                                Else
                                    For i As Integer = FirstLessThan + 1 To PossibleTypes.Length - 1
                                        Dim lessThanCount As Integer = 1
                                        Select Case PossibleTypes.Chars(i)
                                            Case "<"c
                                                lessThanCount += 1
                                            Case ">"c
                                                lessThanCount -= 1
                                        End Select
                                        If lessThanCount = 0 Then
                                            EndIndex = i + 1
                                            Exit For
                                        End If
                                    Next
                                End If
                                Dim typeElement As String = PossibleTypes.Substring(0, EndIndex).ConvertTypeArgumentList.Trim
                                DictionaryTypeElement.Add(ConvertToType(typeElement))
                            Catch ex As Exception
                                Stop
                            End Try
                        End If
                        If EndIndex + 1 < PossibleTypes.Length Then
                            PossibleTypes = PossibleTypes.Substring(EndIndex + 1).Trim
                        Else
                            Exit While
                        End If
                    End While
                    Return Factory.GenericName(Name, Factory.TypeArgumentList(Factory.SeparatedList(DictionaryTypeElement)))
                End If
                ' Could be dictionary or List
                If TypeOf PossibleTupleType Is INamedTypeSymbol AndAlso PossibleName.Contains(",", StringComparison.Ordinal) Then
                    Dim NamedType As INamedTypeSymbol = CType(PossibleTupleType, INamedTypeSymbol)
                    Dim DictionaryTypeElement As New List(Of VBS.TypeSyntax)
                    If Not NamedType.TypeArguments.Any Then
                        Return PredefinedTypeObject
                    End If
                    For Each Element As ITypeSymbol In NamedType.TypeArguments
                        DictionaryTypeElement.Add(Element.ConvertToType())
                    Next
                    Return Factory.GenericName(Name,
                                                     Factory.TypeArgumentList(OpenParenToken,
                                                                                OfKeyword.WithTrailingTrivia(VBSpaceTrivia),
                                                                                Factory.SeparatedList(DictionaryTypeElement),
                                                                                CloseParenToken
                                                                                )
                                                    )
                End If
            End If
            Return ConvertToType(PossibleName)
        End Function

        Friend Function ConvertToType(TypeAsCSString As String, Optional AllowArray As Boolean = True) As VBS.TypeSyntax
            Dim typeString As String = TypeAsCSString.Trim
            Dim arrayRank As String = ""
            If typeString.EndsWith("]", StringComparison.OrdinalIgnoreCase) Then
                Dim IndexOfBracket As Integer
                Dim foundclosedBracker As Boolean = True
                For IndexOfBracket = typeString.Length - 2 To 0 Step -1
                    Select Case typeString.Chars(IndexOfBracket)
                        Case "]"c
                            foundclosedBracker = True
                        Case "["c
                            foundclosedBracker = False
                        Case Else
                            If Not foundclosedBracker Then
                                IndexOfBracket += 1
                                Exit For
                            End If
                    End Select
                Next
                If AllowArray Then
                    arrayRank = typeString.Substring(IndexOfBracket).
                                        Replace("[", "(", StringComparison.Ordinal).
                                        Replace("]", ")", StringComparison.Ordinal)
                End If
                typeString = typeString.Substring(0, IndexOfBracket)
            End If

            If typeString.Contains("<", StringComparison.Ordinal) Then
                typeString = typeString.ConvertTypeArgumentList
            End If
            Dim IndexOf As Integer = typeString.IndexOf("(Of ", StringComparison.OrdinalIgnoreCase)
            If IndexOf >= 0 Then
                Dim Name As String = typeString.Substring(0, IndexOf)
                typeString = typeString.Substring(IndexOf + 3)
                Dim IndexOfLastCloseParen As Integer = typeString.LastIndexOf(")", StringComparison.OrdinalIgnoreCase)
                typeString = typeString.Substring(0, IndexOfLastCloseParen)
                Dim TypeList As New List(Of VBS.TypeSyntax)
                Dim PossibleTypes As String = typeString.Trim
                While PossibleTypes.Length > 0
                    Dim EndIndex As Integer
                    ' Type
                    EndIndex = PossibleTypes.IndexOf(",", StringComparison.Ordinal)
                    Dim FirstLessThan As Integer = PossibleTypes.IndexOf("(", StringComparison.Ordinal)
                    If EndIndex = -1 OrElse FirstLessThan = -1 Then
                        EndIndex = PossibleTypes.Length
                    ElseIf EndIndex > FirstLessThan Then
                        Dim OpenParenCount As Integer = 0
                        For currentIndex As Integer = FirstLessThan To PossibleTypes.Length - 1
                            Select Case PossibleTypes.Substring(currentIndex, 1)
                                Case "("
                                    OpenParenCount += 1
                                Case ")"
                                    OpenParenCount -= 1
                                    EndIndex = currentIndex + 1
                                Case ","
                                    If OpenParenCount = 0 Then
                                        EndIndex = currentIndex
                                        Exit For
                                    End If
                            End Select
                        Next
                    End If
                    TypeList.Add(ConvertToType(PossibleTypes.Substring(0, EndIndex)).WithLeadingTrivia(VBSpaceTrivia))
                    If EndIndex + 1 < PossibleTypes.Length Then
                        PossibleTypes = PossibleTypes.Substring(EndIndex + 1).Trim
                    Else
                        Exit While
                    End If
                End While
                Dim TypeArguemntList As VBS.TypeArgumentListSyntax = Factory.TypeArgumentList(Factory.SeparatedList(TypeList))
                Return Factory.GenericName(Name, TypeArguemntList)
            End If
            If typeString.EndsWith("*", StringComparison.OrdinalIgnoreCase) Then
                Return IntPtrType
            End If
            Select Case typeString.ToUpperInvariant
                Case "BYTE"
                    typeString = "Byte"
                Case "SBYTE"
                    typeString = "SByte"
                Case "INT", "INTEGER"
                    typeString = "Integer"
                Case "UINT", "UINTEGER"
                    typeString = "UInteger"
                Case "SHORT"
                    typeString = "Short"
                Case "USHORT"
                    typeString = "UShort"
                Case "LONG"
                    typeString = "Long"
                Case "ULONG"
                    typeString = "ULong"
                Case "FLOAT"
                    typeString = "Single"
                Case "DOUBLE"
                    typeString = "Double"
                Case "CHAR"
                    typeString = "Char"
                Case "BOOL", "BOOLEAN"
                    typeString = "Boolean"
                Case "OBJECT", "VAR"
                    typeString = "Object"
                Case "STRING"
                    typeString = "String"
                Case "DECIMAL"
                    typeString = "Decimal"
                Case "DATETIME"
                    typeString = "DateTime"
                Case "DATE"
                    typeString = "Date"
                Case "?", "_"
                    typeString = "Object"
                Case Else
                    If typeString.Contains("[", StringComparison.OrdinalIgnoreCase) Then
                        typeString = typeString.ConvertTypeArgumentList
                    End If
                    typeString = MakeVBSafeName(typeString)
            End Select
            Return Factory.ParseTypeName($"{typeString}{arrayRank}")
        End Function

        Public Function GenerateSafeVBIdentifier(csIdentifier As SyntaxToken, Node As CSharpSyntaxNode, Model As SemanticModel) As VBS.NameSyntax
            If Node Is Nothing Then
                Throw New ArgumentNullException(NameOf(Node))
            End If

            Dim keywordKind As VB.SyntaxKind = VB.SyntaxFacts.GetKeywordKind(csIdentifier.ValueText)
            Dim BaseVBIdent As String = csIdentifier.ValueText
            If BaseVBIdent = "_" Then
                BaseVBIdent = "__"
            End If
            Dim isBracketNeeded As Boolean = False
            If VB.SyntaxFacts.IsKeywordKind(keywordKind) Then
                isBracketNeeded = False
                If keywordKind.MatchesKind(VB.SyntaxKind.REMKeyword, VB.SyntaxKind.DelegateKeyword) OrElse csIdentifier.Text.Chars(0) = "@" Then
                    isBracketNeeded = True
                ElseIf TypeOf csIdentifier.Parent?.Parent Is CSS.MemberAccessExpressionSyntax Then
                    isBracketNeeded = CType(csIdentifier.Parent?.Parent, CSS.MemberAccessExpressionSyntax).Expression.ToString.Equals(csIdentifier.ToString, StringComparison.Ordinal)
                ElseIf csIdentifier.Parent.AncestorsAndSelf().OfType(Of CSS.UsingDirectiveSyntax)().FirstOrDefault().IsKind(SyntaxKind.UsingDirective) Then
                    csIdentifier = Factory.Token(keywordKind).WithTriviaFrom(csIdentifier)
                    isBracketNeeded = False
                End If
            End If
            BaseVBIdent = If(isBracketNeeded, $"[{BaseVBIdent}]", BaseVBIdent)
            Dim isField As Boolean = Node.AncestorsAndSelf().OfType(Of CSS.FieldDeclarationSyntax).Any
            Dim symbolEntry As (SyntaxToken, Boolean) = GetSymbolTableEntry(csIdentifier, BaseVBIdent, Node, Model, IsQualifiedNameOrTypeName:=False, isField)
            If isField Then
                Factory.QualifiedName(Factory.IdentifierName(MeKeyword), Factory.IdentifierName(BaseVBIdent))
            End If
            Return Factory.IdentifierName(BaseVBIdent)
        End Function

        ''' <summary>
        ''' Returns Safe VB Name with QualifiedName and TypeName both false
        ''' </summary>
        ''' <param name="id"></param>
        ''' <returns></returns>
        ''' <param name="Node"></param><param name="Model"></param>
        Public Function GenerateSafeVBToken(id As SyntaxToken, Node As CSharpSyntaxNode, Model As SemanticModel) As SyntaxToken
            Return GenerateSafeVBToken(id, Node, Model, IsQualifiedName:=False, IsTypeName:=False)
        End Function

        ''' <summary>
        ''' Returns Safe VB Name
        ''' </summary>
        ''' <param name="id">Original Variable Name</param>
        ''' <param name="Node"></param>
        ''' <param name="Model"></param>
        ''' <param name="IsQualifiedName">True if name is part of a Qualified Name and should not be renamed</param>
        ''' <param name="IsTypeName"></param>
        ''' <returns></returns>
        Public Function GenerateSafeVBToken(id As SyntaxToken, Node As CSharpSyntaxNode, Model As SemanticModel, IsQualifiedName As Boolean, IsTypeName As Boolean) As SyntaxToken
            If Node Is Nothing Then
                Throw New ArgumentNullException(NameOf(Node))
            End If

            Dim keywordKind As VB.SyntaxKind = VB.SyntaxFacts.GetKeywordKind(id.ValueText)
            If IsTypeName Then
                IsQualifiedName = True
            Else
                If VB.SyntaxFacts.IsPredefinedType(keywordKind) Then
                    Return id.MakeIdentifierUnique(Node, Model, IsBracketNeeded:=True, IsQualifiedName)
                End If
            End If

            If VB.SyntaxFacts.IsKeywordKind(keywordKind) Then
                Dim bracketNeeded As Boolean = True
                If keywordKind.MatchesKind(VB.SyntaxKind.REMKeyword, VB.SyntaxKind.DelegateKeyword) OrElse id.Text.Chars(0) = "@" Then
                    bracketNeeded = True
                ElseIf id.Parent Is Nothing Then
                    bracketNeeded = True
                ElseIf TypeOf id.Parent.Parent Is CSS.MemberAccessExpressionSyntax Then
                    bracketNeeded = CType(id.Parent?.Parent, CSS.MemberAccessExpressionSyntax).Expression.ToString.Equals(id.ToString, StringComparison.Ordinal)
                ElseIf id.Parent.AncestorsAndSelf().OfType(Of CSS.UsingDirectiveSyntax)().FirstOrDefault().IsKind(SyntaxKind.UsingDirective) Then
                    id = Factory.Token(keywordKind).WithTriviaFrom(id)
                    bracketNeeded = False
                End If
                Return id.MakeIdentifierUnique(Node, Model, bracketNeeded, IsQualifiedNameOrTypeName:=IsQualifiedName)
            End If

            If id.Parent?.IsParentKind(SyntaxKind.Parameter) Then
                Dim Param As CSS.ParameterSyntax = DirectCast(id.Parent.Parent, CSS.ParameterSyntax)
                Dim MethodDeclaration As CSS.MethodDeclarationSyntax = TryCast(Param.Parent?.Parent, CSS.MethodDeclarationSyntax)
                IsQualifiedName = MethodDeclaration Is Nothing OrElse String.Compare(MethodDeclaration.Identifier.ValueText, id.ValueText, ignoreCase:=True, Globalization.CultureInfo.InvariantCulture) = 0
                IsQualifiedName = IsQualifiedName Or String.Compare(Param.Type.ToString, id.ValueText, ignoreCase:=False, Globalization.CultureInfo.InvariantCulture) = 0
            End If
            Return id.MakeIdentifierUnique(Node, Model, IsBracketNeeded:=False, IsQualifiedName)
        End Function

    End Module
End Namespace