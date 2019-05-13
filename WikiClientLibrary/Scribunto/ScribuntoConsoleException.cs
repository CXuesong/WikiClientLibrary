using System;
using System.Collections.Generic;
using System.Text;

namespace WikiClientLibrary.Scribunto
{
    public class ScribuntoConsoleException : WikiClientException
    {

        private static string MakeMessage(string errorCode, string errorMessage)
        {
            var message = "Error while evaluating expression";
            if (!string.IsNullOrEmpty(errorCode))
                message += ": " + errorCode;
            message += ".";
            if (!string.IsNullOrEmpty(errorMessage))
                message += " " + errorMessage;
            return message;
        }

        public ScribuntoConsoleException(string errorCode, string errorMessage)
            : this(errorCode, errorMessage, null)
        {
        }

        public ScribuntoConsoleException(string errorCode, string errorMessage, ScribuntoEvaluationResult evaluationResult)
            : base(MakeMessage(errorCode, errorMessage))
        {
            ErrorCode = errorCode;
            ErrorMessage = errorMessage;
            EvaluationResult = evaluationResult;
        }

        public ScribuntoEvaluationResult EvaluationResult { get; }

        public string ErrorCode { get; }

        public string ErrorMessage { get; }

    }
}
