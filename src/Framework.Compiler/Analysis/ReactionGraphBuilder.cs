namespace RazorWings.Compiler.Analysis;

using RazorWings.Compiler.Models;

/// <summary>
/// Builds the reactive dependency graph from a parsed component model.
/// Tracks state mutations, DOM dependencies, and event handler relationships.
/// </summary>
public class ReactionGraphBuilder
{
    /// <summary>
    /// Builds a reaction graph from a component model.
    /// </summary>
    /// <param name="component">The parsed component model.</param>
    /// <returns>A ReactionGraph representing the component's reactivity.</returns>
    public ReactionGraph BuildGraph(ComponentModel component)
    {
        var graph = new ReactionGraph();

        if (!component.IsValid)
        {
            graph.IsValid = false;
            graph.Messages.Add("Cannot build graph from invalid component model");
            return graph;
        }

        try
        {
            // 1. Build state-to-selectors mapping from bindings
            BuildStateToSelectorsMapping(component, graph);

            // 2. Build handler-to-mutations mapping from event handlers
            BuildHandlerToMutationsMapping(component, graph);

            // 3. Build computed and structural dependency mappings
            BuildComputedDependencies(component, graph);
            BuildStructuralDependencies(component, graph);

            // 4. Build reverse state-to-handlers mapping
            BuildStateToHandlersMapping(graph);

            // 5. Determine state update order (topological sort)
            ComputeStateUpdateOrder(component, graph);

            graph.IsValid = true;
        }
        catch (Exception ex)
        {
            graph.IsValid = false;
            graph.Messages.Add($"Error building reaction graph: {ex.Message}");
        }

        return graph;
    }

    private static void BuildComputedDependencies(ComponentModel component, ReactionGraph graph)
    {
        foreach (var state in component.StateVariables.Where(s =>
                     s.IsComputed && !string.IsNullOrWhiteSpace(s.ComputedExpression)))
        {
            var dependencies = component.StateVariables
                .Where(candidate => !string.Equals(candidate.Name, state.Name, StringComparison.Ordinal) &&
                    System.Text.RegularExpressions.Regex.IsMatch(
                        state.ComputedExpression!,
                        $@"\b{System.Text.RegularExpressions.Regex.Escape(candidate.Name)}\b"))
                .Select(candidate => candidate.Name)
                .ToHashSet(StringComparer.Ordinal);

            state.Dependencies = dependencies;
            graph.ComputedDependencies[state.Name] = dependencies;
        }
    }

    private static void BuildStructuralDependencies(ComponentModel component, ReactionGraph graph)
    {
        foreach (var node in EnumerateNodes(component.MarkupNodes))
        {
            switch (node)
            {
                case RazorIfNode conditional:
                    graph.StructuralDependencies[$"if:{conditional.LineNumber}"] = conditional.Condition;
                    break;
                case RazorForEachNode loop:
                    graph.StructuralDependencies[$"foreach:{loop.LineNumber}"] = loop.CollectionExpression;
                    break;
            }
        }
    }

    private static IEnumerable<RazorMarkupNode> EnumerateNodes(IEnumerable<RazorMarkupNode> nodes)
    {
        foreach (var node in nodes)
        {
            yield return node;
            foreach (var child in EnumerateNodes(node.Children))
                yield return child;

            if (node is RazorIfNode conditional)
            {
                foreach (var child in EnumerateNodes(conditional.ElseChildren))
                    yield return child;
            }
        }
    }

    /// <summary>
    /// Builds the state-to-DOM-selectors mapping from component bindings.
    /// </summary>
    private void BuildStateToSelectorsMapping(ComponentModel component, ReactionGraph graph)
    {
        // Initialize entry for each state variable
        foreach (var stateVar in component.StateVariables)
        {
            if (!graph.StateToSelectors.ContainsKey(stateVar.Name))
            {
                graph.StateToSelectors[stateVar.Name] = new HashSet<string>();
            }
        }

        foreach (var stateVariable in component.StateVariables)
        {
            if (component.StateToSelectorsMap.TryGetValue(stateVariable.Name, out var selectors) &&
                graph.StateToSelectors.TryGetValue(stateVariable.Name, out var graphSelectors))
            {
                graphSelectors.UnionWith(selectors);
            }
        }
    }

    /// <summary>
    /// Builds the handler-to-mutations mapping.
    /// </summary>
    private void BuildHandlerToMutationsMapping(ComponentModel component, ReactionGraph graph)
    {
        foreach (var handler in component.EventHandlers)
        {
            if (handler.MutatedVariables.Count > 0)
            {
                graph.HandlerToMutations[handler.Name] = new HashSet<string>(handler.MutatedVariables);
            }
        }
    }

    /// <summary>
    /// Builds the reverse mapping: state variables to handlers that mutate them.
    /// </summary>
    private void BuildStateToHandlersMapping(ReactionGraph graph)
    {
        // Initialize entry for each state variable
        foreach (var stateVar in graph.StateToSelectors.Keys)
        {
            if (!graph.StateToHandlers.ContainsKey(stateVar))
            {
                graph.StateToHandlers[stateVar] = new HashSet<string>();
            }
        }

        // Reverse the handler-to-mutations mapping
        foreach (var kvp in graph.HandlerToMutations)
        {
            var handlerName = kvp.Key;
            foreach (var mutation in kvp.Value)
            {
                if (graph.StateToHandlers.TryGetValue(mutation, out var handlers))
                {
                    handlers.Add(handlerName);
                }
            }
        }
    }

    /// <summary>
    /// Computes the topological order for state updates.
    /// This determines the order in which state mutations must be applied to the DOM.
    /// </summary>
    private void ComputeStateUpdateOrder(ComponentModel component, ReactionGraph graph)
    {
        var visited = new HashSet<string>();
        var order = new List<string>();

        foreach (var stateVar in component.StateVariables)
        {
            if (!visited.Contains(stateVar.Name))
            {
                DepthFirstSearch(stateVar.Name, visited, order, graph, component);
            }
        }

        graph.StateUpdateOrder = order;
    }

    /// <summary>
    /// Depth-first search for topological sorting.
    /// </summary>
    private void DepthFirstSearch(string stateVar, HashSet<string> visited, List<string> order,
        ReactionGraph graph, ComponentModel component)
    {
        visited.Add(stateVar);

        // Find handlers that mutate this variable
        if (graph.StateToHandlers.TryGetValue(stateVar, out var handlers))
        {
            foreach (var handler in handlers)
            {
                // Find other variables mutated by this handler
                if (graph.HandlerToMutations.TryGetValue(handler, out var mutations))
                {
                    foreach (var mutation in mutations)
                    {
                        if (!visited.Contains(mutation))
                        {
                            DepthFirstSearch(mutation, visited, order, graph, component);
                        }
                    }
                }
            }
        }

        order.Add(stateVar);
    }

}
