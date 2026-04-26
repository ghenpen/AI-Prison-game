using System;
using System.Collections.Generic;

public enum BTStatus { Success, Failure, Running }

public abstract class BTNode
{
    public abstract BTStatus Tick(BTContext ctx);
}

public class Selector : BTNode
{
    private List<BTNode> _children;
    public Selector(params BTNode[] children) => _children = new List<BTNode>(children);

    public override BTStatus Tick(BTContext ctx)
    {
        foreach (var child in _children)
        {
            var status = child.Tick(ctx);
            if (status != BTStatus.Failure) return status;
        }
        return BTStatus.Failure;
    }
}

public class Sequence : BTNode
{
    private List<BTNode> _children;
    public Sequence(params BTNode[] children) => _children = new List<BTNode>(children);

    public override BTStatus Tick(BTContext ctx)
    {
        foreach (var child in _children)
        {
            var status = child.Tick(ctx);
            if (status != BTStatus.Success) return status;
        }
        return BTStatus.Success;
    }
}

public class Inverter : BTNode
{
    private BTNode _child;
    public Inverter(BTNode child) => _child = child;

    public override BTStatus Tick(BTContext ctx)
    {
        var status = _child.Tick(ctx);
        return status == BTStatus.Success ? BTStatus.Failure : BTStatus.Success;
    }
}


public class MaxRepeats : BTNode
{
    private BTNode _child;
    private string _key;
    private int _max;

    public MaxRepeats(BTNode child, string key, int max)
    {
        _child = child;
        _key = key;
        _max = max;
    }

    public override BTStatus Tick(BTContext ctx)
    {
        int count = ctx.GetCounter(_key);
        if (count >= _max) return BTStatus.Failure; 
        var status = _child.Tick(ctx);
        if (status == BTStatus.Success) ctx.IncrementCounter(_key);
        return status;
    }
}

public class Condition : BTNode
{
    private Func<BTContext, bool> _predicate;
    private string _label;

    public Condition(string label, Func<BTContext, bool> predicate)
    {
        _label = label;
        _predicate = predicate;
    }

    public override BTStatus Tick(BTContext ctx)
        => _predicate(ctx) ? BTStatus.Success : BTStatus.Failure;
}

public class Action : BTNode
{
    private Func<BTContext, BTStatus> _action;
    private string _label;

    public Action(string label, Func<BTContext, BTStatus> action)
    {
        _label = label;
        _action = action;
    }

    public override BTStatus Tick(BTContext ctx) => _action(ctx);
}