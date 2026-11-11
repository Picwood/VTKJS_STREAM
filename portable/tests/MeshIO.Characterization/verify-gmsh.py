"""Optional native reader acceptance: pass the exported fixture directory."""
import sys
from pathlib import Path
import gmsh

root = Path(sys.argv[1])
expected = {
    "selected": ({40, 50, 60}, {42}),
    "all": ({10, 20, 30, 40, 50, 60}, {17, 42}),
    "empty": (set(), set()),
    "mixed": ({10, 20, 30, 40, 50, 60}, {17, 42, 90}),
}
coordinates = {10: (.25, 0, 0), 20: (1.5, 0, 0), 30: (.25, 2.5, 0),
               40: (10, 0, 0), 50: (11, 0, 0), 60: (10, 1, 0)}
connectivity = {17: (2, [10, 20, 30]), 42: (2, [40, 50, 60]), 90: (1, [10, 20])}
gmsh.initialize()
gmsh.option.setNumber("General.Terminal", 0)
try:
    print("Gmsh", gmsh.option.getString("General.Version"))
    for kind, (node_ids, element_ids) in expected.items():
        for culture in ("en-US", "it-IT"):
            path = root / f"{kind}-{culture}.msh"
            gmsh.clear()
            gmsh.open(str(path))
            nodes, values, _ = gmsh.model.mesh.getNodes()
            _, tags, _ = gmsh.model.mesh.getElements()
            assert set(map(int, nodes)) == node_ids, path
            assert {int(x) for block in tags for x in block} == element_ids, path
            for index, node in enumerate(nodes):
                assert tuple(values[3 * index:3 * index + 3]) == coordinates[int(node)], path
            for element in element_ids:
                element_type, node_tags, *_ = gmsh.model.mesh.getElement(element)
                assert (element_type, list(node_tags)) == connectivity[element], path
            print("PASS", path.name)
finally:
    gmsh.finalize()
