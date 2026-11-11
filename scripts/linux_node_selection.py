"""Node-target selection history; independent of GTK and of applied model edits."""


class NodeSelection:
    maximum_nodes = 100000
    maximum_steps = 32

    def __init__(self, nodes=()):
        self.history = [self.checked(nodes)]

    @classmethod
    def checked(cls, nodes):
        if len(nodes) > cls.maximum_nodes or any(type(node) is not int or node <= 0 for node in nodes):
            raise ValueError('Select at most 100000 positive mesh node IDs.')
        return frozenset(nodes)

    @property
    def nodes(self):
        return sorted(self.history[-1])

    def apply(self, nodes, operation='replace'):
        incoming, current = self.checked(nodes), self.history[-1]
        if operation == 'replace': result = incoming
        elif operation == 'add': result = current | incoming
        elif operation == 'subtract': result = current - incoming
        elif operation == 'intersect': result = current & incoming
        elif operation == 'invert': result = incoming - current
        else: raise ValueError('Unknown selection operation.')
        if len(result) > self.maximum_nodes: raise ValueError('Selection exceeds 100000 nodes.')
        if result != current:
            self.history.append(result)
            self.history = self.history[-self.maximum_steps - 1:]

    def undo(self):
        if len(self.history) > 1: self.history.pop()
