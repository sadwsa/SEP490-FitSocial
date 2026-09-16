using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FitSocial.Application.DTOs.Conversations;

namespace FitSocial.Application.Interfaces;

public interface IChatRealtimeNotifier
{
    Task BroadcastMessageAsync(List<Guid> participantUserIds, MessageDto message);
}
