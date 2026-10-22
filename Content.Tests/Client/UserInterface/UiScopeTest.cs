using System;
using System.Collections.Generic;
using Content.Client._RF.UserInterface.Scope;
using NUnit.Framework;

namespace Content.Tests.Client.UserInterface;

[TestFixture]
[TestOf(typeof(UiScope))]
public sealed class UiScopeTest
{
    private sealed class Source
    {
        public event Action<int>? Changed;
        public event Action? Pinged;

        public int Subscribers => Changed?.GetInvocationList().Length ?? 0;
        public int Pingers => Pinged?.GetInvocationList().Length ?? 0;

        public void Raise(int value) => Changed?.Invoke(value);
        public void Ping() => Pinged?.Invoke();
    }

    [Test]
    public void ListenFollowsTheTree()
    {
        var scope = new UiScope();
        var source = new Source();
        var seen = new List<int>();

        scope.Listen<int>(h => source.Changed += h, h => source.Changed -= h, seen.Add);
        Assert.That(source.Subscribers, Is.Zero, "not in the tree yet");

        scope.Enter();
        scope.Enter();
        Assert.That(source.Subscribers, Is.EqualTo(1), "entering twice subscribes once");

        source.Raise(1);
        scope.Exit();
        source.Raise(2);
        Assert.That(source.Subscribers, Is.Zero);
        Assert.That(seen, Is.EqualTo(new[] { 1 }));

        scope.Enter();
        source.Raise(3);
        Assert.That(seen, Is.EqualTo(new[] { 1, 3 }), "subscribes again when it returns to the tree");
    }

    [Test]
    public void ListenWithoutArgumentsAndLateRegistration()
    {
        var scope = new UiScope(active: true);
        var source = new Source();
        var count = 0;

        scope.Listen(h => source.Pinged += h, h => source.Pinged -= h, () => count++);
        Assert.That(source.Pingers, Is.EqualTo(1), "registered inside the tree: subscribed at once");

        source.Ping();
        scope.Exit();
        Assert.That(source.Pingers, Is.Zero);
        Assert.That(count, Is.EqualTo(1));
    }

    [Test]
    public void ListenResolvesTheSourceOnEntry()
    {
        var scope = new UiScope();
        var source = new Source();
        var resolved = 0;
        var seen = new List<int>();

        scope.Listen<Source, int>(() =>
        {
            resolved++;
            return source;
        }, (s, h) => s.Changed += h, (s, h) => s.Changed -= h, seen.Add);

        Assert.That(resolved, Is.Zero, "the source is not looked for until the control is in the tree");

        scope.Enter();
        source.Raise(1);
        Assert.That((resolved, source.Subscribers), Is.EqualTo((1, 1)));

        scope.Exit();
        Assert.That(source.Subscribers, Is.Zero, "removed from the source that was found");

        scope.Enter();
        Assert.That(resolved, Is.EqualTo(2), "found again on a new entry");
        Assert.That(seen, Is.EqualTo(new[] { 1 }));
    }

    [Test]
    public void EnteredRunsOnEveryEntry()
    {
        var scope = new UiScope();
        var count = 0;

        scope.Entered(() => count++);
        Assert.That(count, Is.Zero);

        scope.Enter();
        scope.Exit();
        scope.Enter();
        Assert.That(count, Is.EqualTo(2));

        var late = new UiScope(active: true);
        late.Entered(() => count++);
        Assert.That(count, Is.EqualTo(3), "registered inside the tree: runs at once");
    }

    [Test]
    public void EveryFiresOncePerInterval()
    {
        var scope = new UiScope();
        var count = 0;

        scope.Every(1f, () => count++);
        scope.Enter();

        scope.Update(0.4f, true);
        scope.Update(0.4f, true);
        Assert.That(count, Is.Zero);

        scope.Update(0.4f, true);
        Assert.That(count, Is.EqualTo(1));

        scope.Update(5f, true);
        Assert.That(count, Is.EqualTo(2), "a long frame is not made up for");
    }

    [Test]
    public void EveryRunOnEnterAndOutsideTheTree()
    {
        var scope = new UiScope();
        var count = 0;

        scope.Every(10f, () => count++, runOnEnter: true);
        scope.Update(1f, true);
        Assert.That(count, Is.Zero, "out of the tree nothing runs");

        scope.Enter();
        scope.Update(0.1f, true);
        Assert.That(count, Is.EqualTo(1));

        scope.Update(0.1f, true);
        Assert.That(count, Is.EqualTo(1));

        scope.Exit();
        scope.Enter();
        scope.Update(0.1f, true);
        Assert.That(count, Is.EqualTo(2), "runs again after a new entry");
    }

    [Test]
    public void HiddenControlWaitsForBeingVisible()
    {
        var scope = new UiScope(active: true);
        var every = 0;
        var frames = 0;
        var always = 0;

        scope.Every(1f, () => every++);
        scope.EveryFrame(_ => frames++);
        scope.EveryFrame(_ => always++, whenVisible: false);

        scope.Update(2f, false);
        Assert.That((every, frames, always), Is.EqualTo((0, 0, 1)));

        scope.Update(0.01f, true);
        Assert.That((every, frames, always), Is.EqualTo((1, 1, 2)), "the time of the hidden frames counts");
    }

    [Test]
    public void DeferredRunsOncePerFrame()
    {
        var scope = new UiScope(active: true);
        var runs = 0;
        Action? request = null;
        request = scope.Deferred(() =>
        {
            runs++;

            if (runs == 1)
                request!(); // asks again from inside: the next frame
        });

        request();
        request();
        request();
        Assert.That(runs, Is.Zero, "nothing runs before the frame");

        scope.Update(0.01f, true);
        Assert.That(runs, Is.EqualTo(1));

        scope.Update(0.01f, true);
        Assert.That(runs, Is.EqualTo(2));

        scope.Update(0.01f, true);
        Assert.That(runs, Is.EqualTo(2));
    }

    [Test]
    public void DeferredWaitsForTreeAndVisibility()
    {
        var scope = new UiScope();
        var runs = 0;
        var request = scope.Deferred(() => runs++);

        request();
        scope.Update(0.01f, true);
        Assert.That(runs, Is.Zero, "out of the tree");

        scope.Enter();
        scope.Update(0.01f, false);
        Assert.That(runs, Is.Zero, "hidden");

        scope.Update(0.01f, true);
        Assert.That(runs, Is.EqualTo(1), "the request was kept");
    }

    [Test]
    public void TimerFiresOncePerInterval()
    {
        var timer = new UiTimer(1f);

        Assert.That(timer.Tick(0.5f), Is.False);
        Assert.That(timer.Tick(0.6f), Is.True);
        Assert.That(timer.Tick(0.5f), Is.False);
        Assert.That(new UiTimer(1f, due: true).Tick(0f), Is.True);
    }
}
