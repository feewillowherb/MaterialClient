using System.Collections.Concurrent;
using Volo.Abp.EventBus;
using Volo.Abp.EventBus.Local;

namespace MaterialClient.UI.Tests.Tests;

/// <summary>
///     Lightweight ILocalEventBus test double that dispatches events to subscribers
///     within the same instance. Each test instance holds its own TestLocalEventBus.
/// </summary>
public class TestLocalEventBus : ILocalEventBus
{
    private readonly ConcurrentDictionary<Type, List<Func<object, Task>>> _handlers = new();

    public IDisposable Subscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class
    {
        var type = typeof(TEvent);
        var list = _handlers.GetOrAdd(type, _ => new List<Func<object, Task>>());
        Func<object, Task> wrapper = obj => action((TEvent)obj!);
        lock (list)
        {
            list.Add(wrapper);
        }

        return new DisposableAction(() =>
        {
            lock (list) { list.Remove(wrapper); }
        });
    }

    public IDisposable Subscribe<TEvent>(Action<TEvent> action) where TEvent : class
    {
        return Subscribe<TEvent>(msg =>
        {
            action(msg);
            return Task.CompletedTask;
        });
    }

    public Task PublishAsync<TEvent>(TEvent eventData, bool onUnitOfWorkComplete = true) where TEvent : class
    {
        return PublishAsync(typeof(TEvent), eventData!);
    }

    public Task PublishAsync(Type eventType, object eventData, bool onUnitOfWorkComplete = true)
    {
        if (_handlers.TryGetValue(eventType, out var handlers))
        {
            Func<object, Task>[] copies;
            lock (handlers) { copies = handlers.ToArray(); }
            return Task.WhenAll(copies.Select(h => h(eventData)));
        }

        return Task.CompletedTask;
    }

    public List<EventTypeWithEventHandlerFactories> GetEventHandlerFactories(Type eventType)
    {
        return new List<EventTypeWithEventHandlerFactories>();
    }

    public IDisposable Subscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class =>
        NullDisposable();

    public IDisposable Subscribe<TEvent, THandler>()
        where TEvent : class where THandler : IEventHandler, new() => NullDisposable();

    public IDisposable Subscribe(Type eventType, IEventHandler handler) => NullDisposable();
    public IDisposable Subscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class => NullDisposable();
    public IDisposable Subscribe(Type eventType, IEventHandlerFactory factory) => NullDisposable();
    public void Unsubscribe<TEvent>(Func<TEvent, Task> action) where TEvent : class { }
    public void Unsubscribe<TEvent>(ILocalEventHandler<TEvent> handler) where TEvent : class { }
    public void Unsubscribe(Type eventType, IEventHandler handler) { }
    public void Unsubscribe<TEvent>(IEventHandlerFactory factory) where TEvent : class { }
    public void Unsubscribe(Type eventType, IEventHandlerFactory factory) { }
    public void UnsubscribeAll<TEvent>() where TEvent : class { }
    public void UnsubscribeAll(Type eventType) { }

    private static IDisposable NullDisposable() => new DisposableAction(() => { });

    private class DisposableAction : IDisposable
    {
        private readonly Action _dispose;
        public DisposableAction(Action dispose) => _dispose = dispose;
        public void Dispose() => _dispose();
    }
}
