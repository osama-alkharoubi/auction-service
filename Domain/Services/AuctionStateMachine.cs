using Domain.Entities;
using Domain.Enums;
using Domain.Exceptions;

namespace Domain.Services;
public static class AuctionStateMachine
{
    private static readonly Dictionary<AuctionState, HashSet<AuctionState>> ValidTransitions = new()

    {
        { AuctionState.Draft, new HashSet<AuctionState> { AuctionState.Scheduled, AuctionState.Cancelled } },

        { AuctionState.Scheduled, new HashSet<AuctionState> { AuctionState.Live, AuctionState.Cancelled } },

        { AuctionState.Live, new HashSet<AuctionState> { AuctionState.Closed, AuctionState.Cancelled } },

        { AuctionState.Closed, new HashSet<AuctionState> { AuctionState.Settled, AuctionState.Cancelled } },

        { AuctionState.Settled, new HashSet<AuctionState> { AuctionState.Cancelled } },

        { AuctionState.Cancelled, new HashSet<AuctionState>() }

    };
    public static bool CanTransition(AuctionState currentState, AuctionState nextState)
    {

        return ValidTransitions.TryGetValue(currentState, out var allowedStates)

               && allowedStates.Contains(nextState);

    }



    public static AuctionAuditLog ChangeState
        (
     Auction auction,

     AuctionState newState,

     Guid actorUserId,

     bool isAdmin,

     DateTime currentUtcNow,

     string reason = "",

     string metadata = "{}")

    {
        var oldState = auction.StateId;
        if (!CanTransition(oldState, newState))
        {
            throw new InvalidStateTransitionException(oldState.ToString(), newState.ToString());
        }

        switch (newState)
        {
            case AuctionState.Live when currentUtcNow < auction.StartsUtc:
                throw new InvalidStateTransitionException(oldState.ToString(), newState.ToString(), "StartsUtc has not been reached yet.");

            case AuctionState.Closed when currentUtcNow < auction.EndsUtc && !isAdmin:

                throw new InvalidStateTransitionException(oldState.ToString(), newState.ToString(), "EndsUtc has not been reached yet and non-admin cannot force close.");
            case AuctionState.Cancelled:

                if (!isAdmin)
                {
                    throw new InvalidStateTransitionException(oldState.ToString(), newState.ToString(), "Only admins can cancel auctions.");
                }

                if (string.IsNullOrWhiteSpace(reason))
                {
                    throw new InvalidStateTransitionException(oldState.ToString(), newState.ToString(), "Cancellation reason is required.");
                }
                break;
        }
        auction.StateId = newState;

        return new AuctionAuditLog
        {
            Id = Guid.NewGuid(),

            AuctionId = auction.Id,

            BeforeStateId = oldState,

            AfterStateId = newState,

            ActorUserId = actorUserId,

            Reason = reason,

            Metadata = string.IsNullOrWhiteSpace(metadata) ? "{}" : metadata
        };
    }
}