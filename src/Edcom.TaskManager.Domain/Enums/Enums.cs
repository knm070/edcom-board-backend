namespace Edcom.TaskManager.Domain.Enums;

public enum OrgRole
{
    OrgManager = 1,
    Employer   = 2,
}

public enum InviteStatus
{
    Pending  = 0,
    Accepted = 1,
    Expired  = 2,
}

public enum BoardType
{
    Kanban = 0,
    Scrum  = 1,
}

public enum SprintStatus
{
    Planning  = 0,
    Active    = 1,
    Completed = 2,
}

public enum TicketType
{
    Task    = 0,
    Bug     = 1,
    Feature = 2,
}

public enum Priority
{
    Low      = 0,
    Medium   = 1,
    High     = 2,
    Critical = 3,
}

public enum WorkflowStatusBaseType
{
    ToDo       = 0,
    InProgress = 1,
    InReview   = 2,
    Done       = 3,
    Custom     = 4,
    Backlog    = 5,
}

public enum ActivityAction
{
    Created            = 0,
    StatusChanged      = 1,
    AssigneeChanged    = 2,
    PriorityChanged    = 3,
    TitleChanged       = 4,
    DescriptionChanged = 5,
    SprintChanged      = 6,
    EpicChanged        = 7,
    DueDateChanged     = 8,
    CommentAdded       = 9,
}

public enum NotificationType
{
    OrgApproved         = 0,
    OrgRejected         = 1,
    TicketAssigned      = 2,
    TicketStatusChanged = 3,
    CommentAdded        = 4,
    SprintStarted       = 5,
    SprintCompleted     = 6,
    InviteReceived      = 7,
}
