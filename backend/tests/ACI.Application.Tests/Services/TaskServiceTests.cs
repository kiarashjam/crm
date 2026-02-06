using ACI.Application.Common;
using ACI.Application.DTOs;
using ACI.Application.Interfaces;
using ACI.Application.Services;
using ACI.Domain.Entities;
using ACI.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace ACI.Application.Tests.Services;

/// <summary>
/// Unit tests for TaskService.
/// </summary>
public class TaskServiceTests
{
    private readonly Mock<ITaskRepository> _taskRepositoryMock;
    private readonly Mock<ILogger<TaskService>> _loggerMock;
    private readonly TaskService _sut;

    public TaskServiceTests()
    {
        _taskRepositoryMock = new Mock<ITaskRepository>();
        _loggerMock = new Mock<ILogger<TaskService>>();

        _sut = new TaskService(
            _taskRepositoryMock.Object,
            _loggerMock.Object);
    }

    #region GetTasksAsync Tests

    [Fact]
    public async Task GetTasksAsync_ReturnsTasks_WhenTasksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var tasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Task 1", Status = Domain.Enums.TaskStatus.Todo },
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Task 2", Status = Domain.Enums.TaskStatus.InProgress }
        };
        var filters = new TaskFilterParams();

        _taskRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        // Act
        var result = await _sut.GetTasksAsync(userId, orgId, filters);

        // Assert
        result.Should().HaveCount(2);
        result[0].Title.Should().Be("Task 1");
        result[1].Title.Should().Be("Task 2");
    }

    [Fact]
    public async Task GetTasksAsync_ReturnsEmptyList_WhenNoTasksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var filters = new TaskFilterParams();

        _taskRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<TaskItem>());

        // Act
        var result = await _sut.GetTasksAsync(userId, orgId, filters);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetTasksAsync_FiltersOverdueTasks_WhenOverdueOnlyIsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var overdueTasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Overdue Task", DueDateUtc = DateTime.UtcNow.AddDays(-1) }
        };
        var filters = new TaskFilterParams { OverdueOnly = true };

        _taskRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overdueTasks);

        // Act
        var result = await _sut.GetTasksAsync(userId, orgId, filters);

        // Assert
        result.Should().HaveCount(1);
        result[0].Title.Should().Be("Overdue Task");
    }

    [Fact]
    public async Task GetTasksAsync_FiltersByStatus_WhenStatusProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var completedTasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Completed Task", Status = Domain.Enums.TaskStatus.Completed }
        };
        var filters = new TaskFilterParams { Status = "completed" };

        _taskRepositoryMock
            .Setup(r => r.GetByStatusAsync(userId, orgId, Domain.Enums.TaskStatus.Completed, It.IsAny<CancellationToken>()))
            .ReturnsAsync(completedTasks);

        // Act
        var result = await _sut.GetTasksAsync(userId, orgId, filters);

        // Assert
        result.Should().HaveCount(1);
        result[0].Status.Should().Be("completed");
    }

    #endregion

    #region GetByIdAsync Tests

    [Fact]
    public async Task GetByIdAsync_ReturnsTask_WhenTaskExists()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var task = new TaskItem
        {
            Id = taskId,
            UserId = userId,
            Title = "Test Task",
            Description = "Task Description",
            Status = Domain.Enums.TaskStatus.Todo
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(task);

        // Act
        var result = await _sut.GetByIdAsync(taskId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Id.Should().Be(taskId);
        result.Value.Title.Should().Be("Test Task");
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsNotFoundError_WhenTaskDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var result = await _sut.GetByIdAsync(taskId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Task.NotFound");
    }

    #endregion

    #region CreateAsync Tests

    [Fact]
    public async Task CreateAsync_ReturnsTask_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateTaskRequest
        {
            Title = "New Task",
            Description = "Description",
            DueDateUtc = DateTime.UtcNow.AddDays(7),
            Status = "todo",
            Priority = "high",
            LeadId = null,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            ReminderDateUtc = null,
            Notes = null
        };

        _taskRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, CancellationToken _) => t);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("New Task");
        result.Value.Status.Should().Be("todo");
        result.Value.Priority.Should().Be("high");
    }

    [Fact]
    public async Task CreateAsync_ReturnsError_WhenTitleIsEmpty()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateTaskRequest
        {
            Title = "",
            Description = null,
            DueDateUtc = null,
            Status = "todo",
            Priority = null,
            LeadId = null,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            ReminderDateUtc = null,
            Notes = null
        };

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Task.TitleRequired");
    }

    [Fact]
    public async Task CreateAsync_DefaultsStatusToTodo_WhenNotProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var request = new CreateTaskRequest
        {
            Title = "Default Status Task",
            Description = null,
            DueDateUtc = null,
            Status = null,
            Priority = null,
            LeadId = null,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            ReminderDateUtc = null,
            Notes = null
        };

        _taskRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, CancellationToken _) => t);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("todo");
    }

    [Fact]
    public async Task CreateAsync_LinksToLead_WhenLeadIdProvided()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var request = new CreateTaskRequest
        {
            Title = "Lead Task",
            Description = null,
            DueDateUtc = null,
            Status = "todo",
            Priority = null,
            LeadId = leadId,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            ReminderDateUtc = null,
            Notes = null
        };

        _taskRepositoryMock
            .Setup(r => r.AddAsync(It.IsAny<TaskItem>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, CancellationToken _) => t);

        // Act
        var result = await _sut.CreateAsync(userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.LeadId.Should().Be(leadId);
    }

    #endregion

    #region UpdateAsync Tests

    [Fact]
    public async Task UpdateAsync_ReturnsUpdatedTask_WhenValidRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new TaskItem
        {
            Id = taskId,
            UserId = userId,
            Title = "Old Task",
            Status = Domain.Enums.TaskStatus.Todo
        };
        var request = new UpdateTaskRequest
        {
            Title = "Updated Task",
            Description = "Updated Description",
            Status = "in_progress",
            Priority = "medium",
            DueDateUtc = null,
            ReminderDateUtc = null,
            LeadId = null,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            Notes = null,
            Completed = null,
            ClearDueDate = null,
            ClearReminderDate = null,
            ClearLead = null,
            ClearDeal = null,
            ClearContact = null,
            ClearAssignee = null
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, Guid _, Guid? _, CancellationToken _) => t);

        // Act
        var result = await _sut.UpdateAsync(taskId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Title.Should().Be("Updated Task");
        result.Value.Status.Should().Be("in_progress");
    }

    [Fact]
    public async Task UpdateAsync_ReturnsNotFoundError_WhenTaskDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var request = new UpdateTaskRequest
        {
            Title = "Updated Title",
            Description = null,
            Status = null,
            Priority = null,
            DueDateUtc = null,
            ReminderDateUtc = null,
            LeadId = null,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            Notes = null,
            Completed = null,
            ClearDueDate = null,
            ClearReminderDate = null,
            ClearLead = null,
            ClearDeal = null,
            ClearContact = null,
            ClearAssignee = null
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var result = await _sut.UpdateAsync(taskId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Task.NotFound");
    }

    [Fact]
    public async Task UpdateAsync_SetsCompletedAtUtc_WhenStatusChangesToCompleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new TaskItem
        {
            Id = taskId,
            UserId = userId,
            Title = "Task",
            Status = Domain.Enums.TaskStatus.Todo,
            CompletedAtUtc = null
        };
        var request = new UpdateTaskRequest
        {
            Title = null,
            Description = null,
            Status = "completed",
            Priority = null,
            DueDateUtc = null,
            ReminderDateUtc = null,
            LeadId = null,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            Notes = null,
            Completed = null,
            ClearDueDate = null,
            ClearReminderDate = null,
            ClearLead = null,
            ClearDeal = null,
            ClearContact = null,
            ClearAssignee = null
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, Guid _, Guid? _, CancellationToken _) => t);

        // Act
        var result = await _sut.UpdateAsync(taskId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Completed.Should().BeTrue();
        result.Value.CompletedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task UpdateAsync_ClearsDueDate_WhenClearDueDateIsTrue()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new TaskItem
        {
            Id = taskId,
            UserId = userId,
            Title = "Task with Due Date",
            DueDateUtc = DateTime.UtcNow.AddDays(5)
        };
        var request = new UpdateTaskRequest
        {
            Title = null,
            Description = null,
            Status = null,
            Priority = null,
            DueDateUtc = null,
            ReminderDateUtc = null,
            LeadId = null,
            DealId = null,
            ContactId = null,
            AssigneeId = null,
            Notes = null,
            Completed = null,
            ClearDueDate = true,
            ClearReminderDate = null,
            ClearLead = null,
            ClearDeal = null,
            ClearContact = null,
            ClearAssignee = null
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, Guid _, Guid? _, CancellationToken _) => t);

        // Act
        var result = await _sut.UpdateAsync(taskId, userId, orgId, request);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DueDateUtc.Should().BeNull();
    }

    #endregion

    #region UpdateStatusAsync Tests

    [Fact]
    public async Task UpdateStatusAsync_ReturnsUpdatedTask_WhenValidStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new TaskItem
        {
            Id = taskId,
            UserId = userId,
            Title = "Task",
            Status = Domain.Enums.TaskStatus.Todo
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, Guid _, Guid? _, CancellationToken _) => t);

        // Act
        var result = await _sut.UpdateStatusAsync(taskId, userId, orgId, "in_progress");

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Status.Should().Be("in_progress");
    }

    [Fact]
    public async Task UpdateStatusAsync_ReturnsNotFoundError_WhenTaskDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem?)null);

        // Act
        var result = await _sut.UpdateStatusAsync(taskId, userId, orgId, "completed");

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Task.NotFound");
    }

    #endregion

    #region DeleteAsync Tests

    [Fact]
    public async Task DeleteAsync_ReturnsSuccess_WhenTaskDeleted()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        _taskRepositoryMock
            .Setup(r => r.DeleteAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.DeleteAsync(taskId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteAsync_ReturnsNotFoundError_WhenTaskDoesNotExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        _taskRepositoryMock
            .Setup(r => r.DeleteAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // Act
        var result = await _sut.DeleteAsync(taskId, userId, orgId);

        // Assert
        result.IsSuccess.Should().BeFalse();
        result.Error.Code.Should().Be("Task.NotFound");
    }

    #endregion

    #region AssignTaskAsync Tests

    [Fact]
    public async Task AssignTaskAsync_ReturnsUpdatedTask_WhenValidAssignee()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var assigneeId = Guid.NewGuid();
        var existingTask = new TaskItem
        {
            Id = taskId,
            UserId = userId,
            Title = "Task",
            AssigneeId = null
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, Guid _, Guid? _, CancellationToken _) => t);

        // Act
        var result = await _sut.AssignTaskAsync(taskId, userId, orgId, assigneeId);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssigneeId.Should().Be(assigneeId);
    }

    [Fact]
    public async Task AssignTaskAsync_ClearsAssignee_WhenAssigneeIdIsNull()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        var existingTask = new TaskItem
        {
            Id = taskId,
            UserId = userId,
            Title = "Task",
            AssigneeId = Guid.NewGuid()
        };

        _taskRepositoryMock
            .Setup(r => r.GetByIdWithRelationsAsync(taskId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingTask);

        _taskRepositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<TaskItem>(), userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TaskItem t, Guid _, Guid? _, CancellationToken _) => t);

        // Act
        var result = await _sut.AssignTaskAsync(taskId, userId, orgId, null);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.AssigneeId.Should().BeNull();
    }

    #endregion

    #region GetTasksByLeadIdAsync Tests

    [Fact]
    public async Task GetTasksByLeadIdAsync_ReturnsTasks_WhenTasksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var leadId = Guid.NewGuid();
        var tasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Lead Task 1", LeadId = leadId },
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Lead Task 2", LeadId = leadId }
        };

        _taskRepositoryMock
            .Setup(r => r.GetByLeadIdAsync(leadId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        // Act
        var result = await _sut.GetTasksByLeadIdAsync(leadId, userId, orgId);

        // Assert
        result.Should().HaveCount(2);
        result[0].LeadId.Should().Be(leadId);
    }

    #endregion

    #region GetTasksByDealIdAsync Tests

    [Fact]
    public async Task GetTasksByDealIdAsync_ReturnsTasks_WhenTasksExist()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var dealId = Guid.NewGuid();
        var tasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Deal Task 1", DealId = dealId }
        };

        _taskRepositoryMock
            .Setup(r => r.GetByDealIdAsync(dealId, userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        // Act
        var result = await _sut.GetTasksByDealIdAsync(dealId, userId, orgId);

        // Assert
        result.Should().HaveCount(1);
        result[0].DealId.Should().Be(dealId);
    }

    #endregion

    #region GetStatsAsync Tests

    [Fact]
    public async Task GetStatsAsync_ReturnsCorrectStats()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var orgId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var tasks = new List<TaskItem>
        {
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Todo Task", Status = Domain.Enums.TaskStatus.Todo },
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "In Progress Task", Status = Domain.Enums.TaskStatus.InProgress },
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Completed Task", Status = Domain.Enums.TaskStatus.Completed },
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "Overdue Task", Status = Domain.Enums.TaskStatus.Todo, DueDateUtc = now.AddDays(-1) },
            new() { Id = Guid.NewGuid(), UserId = userId, Title = "High Priority Task", Status = Domain.Enums.TaskStatus.Todo, Priority = TaskPriority.High }
        };

        _taskRepositoryMock
            .Setup(r => r.GetByUserIdAsync(userId, orgId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(tasks);

        // Act
        var result = await _sut.GetStatsAsync(userId, orgId);

        // Assert
        result.Total.Should().Be(5);
        result.Todo.Should().Be(3); // 3 Todo tasks
        result.InProgress.Should().Be(1);
        result.Completed.Should().Be(1);
        result.Overdue.Should().Be(1);
        result.HighPriority.Should().Be(1);
    }

    #endregion
}
