﻿' Licensed to the .NET Foundation under one or more agreements.
' The .NET Foundation licenses this file to you under the MIT license.
' See the LICENSE file in the project root for more information.

Imports Microsoft.CodeAnalysis

Imports CS = Microsoft.CodeAnalysis.CSharp

Namespace CSharpToVBCodeConverter.ToVisualBasic
    Friend NotInheritable Class CommonConversions
        Private ReadOnly _semanticModel As SemanticModel
        Sub New(Model As SemanticModel)
            _semanticModel = Model
        End Sub

        Private Function GetSymbol(syntax As CS.CSharpSyntaxNode) As ISymbol
            Return If(syntax.SyntaxTree Is _semanticModel.SyntaxTree, _semanticModel.GetSymbolInfo(syntax).Symbol, Nothing)
        End Function

        Public Function IsEventHandlerIdentifier(syntax As CS.CSharpSyntaxNode) As Boolean
            Return Me.GetSymbol(syntax).IsKind(SymbolKind.Event)
        End Function

    End Class

End Namespace