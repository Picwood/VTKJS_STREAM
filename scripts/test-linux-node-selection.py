import unittest
from linux_node_selection import NodeSelection


class SelectionChecks(unittest.TestCase):
    def test_operations_use_resolved_nodes(self):
        selection = NodeSelection([1, 2, 3, 4])
        selection.apply([3, 4, 5, 6], 'add')
        self.assertEqual(selection.nodes, [1, 2, 3, 4, 5, 6])
        selection.apply([3, 4, 5, 6], 'subtract')
        self.assertEqual(selection.nodes, [1, 2])  # Shared nodes are removed too.
        selection.undo()
        selection.apply([2, 3, 8], 'intersect')
        self.assertEqual(selection.nodes, [2, 3])
        selection.apply(list(range(1, 9)), 'invert')
        self.assertEqual(selection.nodes, [1, 4, 5, 6, 7, 8])
        selection.apply([])
        self.assertEqual(selection.nodes, [])
        selection.undo()
        self.assertEqual(selection.nodes, [1, 4, 5, 6, 7, 8])

    def test_history_initialization_and_limits(self):
        selection = NodeSelection([3, 1, 3])
        self.assertEqual(selection.nodes, [1, 3])
        selection.apply([1, 3])
        self.assertEqual(len(selection.history), 1)
        for node in range(50): selection.apply([node + 1])
        self.assertEqual(len(selection.history), 33)
        for _ in range(40): selection.undo()
        self.assertEqual(len(selection.history), 1)

    def test_failures_preserve_selection(self):
        selection = NodeSelection([1])
        for nodes, operation in (([0], 'add'), ([True], 'add'), ([2], 'unknown'), (list(range(1, 100002)), 'replace')):
            with self.assertRaises(ValueError): selection.apply(nodes, operation)
            self.assertEqual(selection.nodes, [1])
        selection.apply(list(range(2, 100002)))
        with self.assertRaises(ValueError): selection.apply([1], 'add')
        self.assertEqual(len(selection.nodes), 100000)


if __name__ == '__main__': unittest.main()
