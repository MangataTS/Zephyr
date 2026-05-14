using ZephyrDesktop.Models;

namespace ZephyrDesktop.Events;

public sealed class LoginSuccessEvent
{
    public LoginResponse LoginResponse { get; }

    public LoginSuccessEvent(LoginResponse loginResponse)
    {
        LoginResponse = loginResponse;
    }
}

public sealed class NoteCreatedEvent
{
    public NoteDto Note { get; }

    public NoteCreatedEvent(NoteDto note)
    {
        Note = note;
    }
}

public sealed class NoteCompletedEvent
{
    public string NoteId { get; }

    public NoteCompletedEvent(string noteId)
    {
        NoteId = noteId;
    }
}

public sealed class NoteMinimizedEvent
{
    public string NoteId { get; }

    public NoteMinimizedEvent(string noteId)
    {
        NoteId = noteId;
    }
}

public sealed class NoteAssignedEvent
{
    public string NoteId { get; }
    public string Title { get; }
    public string FromName { get; }

    public NoteAssignedEvent(string noteId, string title, string fromName)
    {
        NoteId = noteId;
        Title = title;
        FromName = fromName;
    }
}

public sealed class NoteRemindedEvent
{
    public string NoteId { get; }
    public string ReminderName { get; }
    public string Message { get; }
    public string ColorStatus { get; }

    public NoteRemindedEvent(string noteId, string reminderName, string message, string colorStatus)
    {
        NoteId = noteId;
        ReminderName = reminderName;
        Message = message;
        ColorStatus = colorStatus;
    }
}

public sealed class NoteArchivedEvent
{
    public string NoteId { get; }
    public string ArchivedBy { get; }

    public NoteArchivedEvent(string noteId, string archivedBy)
    {
        NoteId = noteId;
        ArchivedBy = archivedBy;
    }
}

public sealed class NoteRestoredEvent
{
    public string NoteId { get; }
    public string Title { get; }
    public string Content { get; }
    public string ColorStatus { get; }

    public NoteRestoredEvent(string noteId, string title, string content, string colorStatus)
    {
        NoteId = noteId;
        Title = title;
        Content = content;
        ColorStatus = colorStatus;
    }
}

public sealed class ForceLogoutEvent;

public sealed class NetworkStatusEvent
{
    public bool IsOnline { get; }

    public NetworkStatusEvent(bool isOnline)
    {
        IsOnline = isOnline;
    }
}

public sealed class SyncTriggerEvent;

public sealed class FocusNoteEvent
{
    public string NoteId { get; }

    public FocusNoteEvent(string noteId)
    {
        NoteId = noteId;
    }
}

