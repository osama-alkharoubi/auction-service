using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Domain.Exceptions
{
    public class InvalidStateTransitionException : Exception
    {
        public InvalidStateTransitionException(string fromState, string toState, string reason = "")
            : base(string.IsNullOrWhiteSpace(reason)
                ? $"Cannot transition auction from {fromState} to {toState}."
                : $"Cannot transition auction from {fromState} to {toState}. Reason: {reason}")
        {
        }
    }
}
