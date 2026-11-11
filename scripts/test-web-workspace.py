#!/usr/bin/env python3
"""HTTP acceptance of the real portable web host and isolated production renderers. Build first."""
import http.cookiejar
import hashlib
import json
import os
from pathlib import Path
import socket
import subprocess
import tempfile
import time
import urllib.error
import urllib.request
import uuid

ROOT = Path(__file__).resolve().parents[1]
MESH = b"""*NODE
1,0,0,0
2,1,0,0
3,1,1,0
4,0,1,0
5,0,0,1
6,1,0,1
7,1,1,1
8,0,1,1
*ELEMENT,TYPE=C3D8,ELSET=SOLID
1,1,2,3,4,5,6,7,8
*NSET,NSET=LEFT
1,4,5,8
*NSET,NSET=RIGHT
2,3,6,7
"""


class Browser:
    def __init__(self, base):
        self.base = base
        self.jar = http.cookiejar.CookieJar()
        self.client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(self.jar))
        self.revision = 0

    def request(self, path, body=None, method=None, headers=None, expected=200):
        method = method or ("POST" if body is not None else "GET")
        request_headers = {"X-Workspace-Request": "1", "If-Match": str(self.revision)}
        if isinstance(body, dict):
            body = json.dumps(body).encode()
            request_headers["Content-Type"] = "application/json"
        request_headers.update(headers or {})
        request = urllib.request.Request(self.base + path, data=body, method=method, headers=request_headers)
        try:
            response = self.client.open(request, timeout=45)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            data = response.read()
            assert response.code == expected, (path, response.code, expected, data[:400])
            return data

    def describe(self):
        state = json.loads(self.request("/api/workspace"))
        self.revision = state["revision"]
        return state

    def edit(self, **edit):
        self.request("/api/workspace/edit", edit)
        return self.describe()


def material_lifecycle(browser):
    state = browser.describe()
    original_id = next(item["id"] for item in state["materialIdentities"] if item["name"] == "ELASTIC")
    section = state["sections"][0]["section"]
    before = browser.request("/api/workspace/download/project")
    create = {"operationId": str(uuid.uuid4()), "expectedRevision": browser.revision,
              "name": "WEB_NEW", "youngsModulus": 500, "poissonsRatio": .2}
    created = json.loads(browser.request("/api/workspace/commands/material/create", create))
    material_id = created["receipt"]["materialId"]
    browser.describe()
    assert created["receipt"]["kind"] == "material.elastic.create/1" and material_id != original_id
    assert created["receipt"]["sourceSha256"].lower() == hashlib.sha256(before).hexdigest()
    assert created["receipt"]["resultSha256"].lower() == hashlib.sha256(browser.request("/api/workspace/download/project")).hexdigest()
    retry_headers = {"If-Match": str(create["expectedRevision"])}
    assert json.loads(browser.request("/api/workspace/commands/material/create", create, headers=retry_headers)) == created
    browser.request("/api/workspace/commands/material/create", dict(create, name="OTHER"), headers=retry_headers, expected=409)
    browser.request("/api/workspace/commands/material/create", dict(create, operationId=str(uuid.uuid4())), headers=retry_headers, expected=409)
    preserved = browser.request("/api/workspace/download/project")
    for name in [" WEB_BAD ", "Bad Name", "123", "web_new"]:
        browser.request("/api/workspace/commands/material/create", dict(create, operationId=str(uuid.uuid4()), expectedRevision=browser.revision, name=name), expected=400)
    assert preserved == browser.request("/api/workspace/download/project")
    browser.request("/api/workspace/commands/material/assign", {"operationId": str(uuid.uuid4()), "expectedRevision": browser.revision,
                    "sectionName": section, "materialId": material_id})
    assert browser.describe()["sections"][0]["material"] == "WEB_NEW"
    preserved = browser.request("/api/workspace/download/project")
    commands = json.loads(browser.request("/api/workspace/commands"))
    browser.request("/api/workspace/commands/material/remove", {"operationId": str(uuid.uuid4()), "expectedRevision": browser.revision,
                    "materialId": material_id}, expected=400)
    assert preserved == browser.request("/api/workspace/download/project")
    assert commands == json.loads(browser.request("/api/workspace/commands"))
    browser.edit(action="section-assign", name=section, region="ELASTIC")
    assert json.loads(browser.request("/api/workspace/commands"))[-1]["kind"] == "material.assign-solid-section/1"
    remove = {"operationId": str(uuid.uuid4()), "expectedRevision": browser.revision, "materialId": material_id}
    browser.request("/api/workspace/commands/material/remove", dict(remove, cascade=True), expected=400)
    removed = json.loads(browser.request("/api/workspace/commands/material/remove", remove))
    assert removed["receipt"]["kind"] == "material.remove-unused/1" and removed["receipt"]["materialId"] == material_id
    assert all(item["id"] != material_id for item in browser.describe()["materialIdentities"])
    browser.request("/api/workspace/undo", b"")
    assert any(item["id"] == material_id for item in browser.describe()["materialIdentities"])
    retry_remove = json.loads(browser.request("/api/workspace/commands/material/remove", remove,
                              headers={"If-Match": str(remove["expectedRevision"])}))
    assert retry_remove["receipt"] == removed["receipt"] and retry_remove["revision"] == browser.revision
    assert any(item["id"] == material_id for item in browser.describe()["materialIdentities"])
    browser.request("/api/workspace/redo", b"")
    browser.describe()
    replacement = json.loads(browser.request("/api/workspace/commands/material/create",
                            dict(create, operationId=str(uuid.uuid4()), expectedRevision=browser.revision)))
    replacement_id = replacement["receipt"]["materialId"]
    assert replacement_id != material_id
    browser.describe()
    assert json.loads(browser.request("/api/workspace/commands/material/create", create, headers=retry_headers))["receipt"] == created["receipt"]
    browser.request("/api/workspace/commands/material/remove", remove, headers={"If-Match": str(remove["expectedRevision"])})
    browser.request("/api/workspace/commands/material/remove", dict(remove, operationId=str(uuid.uuid4()), expectedRevision=browser.revision), expected=409)
    assert any(item["id"] == replacement_id for item in browser.describe()["materialIdentities"])
    browser.request("/api/workspace/commands/material/remove", {"operationId": str(uuid.uuid4()), "expectedRevision": browser.revision,
                    "materialId": replacement_id})
    assert len(browser.describe()["materialIdentities"]) == 1


def main():
    with socket.socket() as probe:
        probe.bind(("127.0.0.1", 0))
        port = probe.getsockname()[1]
    env = dict(os.environ, PREPOMAX_WEB_PORT=str(port))
    with tempfile.TemporaryFile() as log:
        process = subprocess.Popen(["bash", "scripts/start-web-workspace.sh"], cwd=ROOT, env=env, stdout=log, stderr=log)
        base = f"http://localhost:{port}"
        try:
            a, b, anonymous = Browser(base), Browser(base), Browser(base)
            deadline = time.monotonic() + 30
            while True:
                try:
                    anonymous.request("/workspace.html")
                    break
                except (urllib.error.URLError, ConnectionError):
                    if process.poll() is not None or time.monotonic() >= deadline:
                        log.seek(0)
                        raise RuntimeError(log.read().decode())
                    time.sleep(.1)
            anonymous.request("/api/workspace", expected=401)
            a.request("/api/workspace", b"", headers={"Origin": "https://example.invalid"}, expected=403)
            for browser in (a, b):
                browser.request("/api/workspace", b"", expected=204)
            b.request("/api/workspace/edit", {"action": "section-assign", "name": "solid", "region": "ELASTIC"}, expected=409)
            b.request("/api/workspace/commands/material/create", {"operationId": str(uuid.uuid4()), "expectedRevision": 0,
                      "name": "EMPTY", "youngsModulus": 500, "poissonsRatio": .2}, expected=409)
            assert not b.describe()["hasProject"] and json.loads(b.request("/api/workspace/commands")) == []
            a.request("/api/workspace/import", MESH, headers={"X-File-Extension": ".inp", "X-Youngs-Modulus": "1000", "X-Poissons-Ratio": "0"})
            state = a.describe()
            a.request("/api/parity/snapshot", expected=404)
            assert state["summary"]["nodes"] == 8 and not b.describe()["hasProject"]
            assert state["tree"][0]["children"][0]["name"] == "Mesh"
            assert not state["fields"]
            before = a.request("/api/workspace/download/project")
            a.request("/api/workspace/edit", {"action": "material-save", "name": "ELASTIC", "e": 3000, "nu": 0}, headers={"If-Match": "0"}, expected=409)
            a.request("/api/workspace/edit", {"action": "nodeset-add", "name": "INVALID", "nodes": [9999]}, expected=400)
            assert before == a.request("/api/workspace/download/project")
            command = {"operationId": str(uuid.uuid4()), "expectedRevision": a.revision,
                       "materialName": "ELASTIC", "youngsModulus": 2000}
            reply = json.loads(a.request("/api/workspace/commands/material", command))
            receipt = reply["receipt"]
            state = a.describe()
            assert state["materials"][0]["youngsModulus"] == 2000
            assert state["materials"][0]["poissonsRatio"] == 0
            assert state["currentCommandId"] == command["operationId"]
            edited = a.request("/api/workspace/download/project")
            assert receipt["sourceRevision"] == command["expectedRevision"] and receipt["committedRevision"] == a.revision
            assert receipt["sourceSha256"].lower() == hashlib.sha256(before).hexdigest()
            assert receipt["resultSha256"].lower() == hashlib.sha256(edited).hexdigest()
            assert json.loads(a.request("/api/workspace/commands")) == [receipt]
            assert json.loads(b.request("/api/workspace/commands")) == []
            anonymous.request("/api/workspace/commands", expected=401)
            retry_headers = {"If-Match": str(command["expectedRevision"])}
            assert json.loads(a.request("/api/workspace/commands/material", command, headers=retry_headers)) == reply
            a.request("/api/workspace/commands/material", dict(command, youngsModulus=3000), headers=retry_headers, expected=409)
            a.request("/api/workspace/commands/material", dict(command, operationId=str(uuid.uuid4())), headers=retry_headers, expected=409)
            # Unknown fields are rejected instead of accepting a silently incomplete patch.
            fresh = dict(command, operationId=str(uuid.uuid4()), expectedRevision=a.revision)
            a.request("/api/workspace/commands/material", dict(fresh, density=7.8), expected=400)
            a.request("/api/workspace/commands/material", dict(fresh, youngsModulus=-1), expected=400)
            a.request("/api/workspace/commands/material", fresh, headers={"If-Match": "0"}, expected=409)
            assert edited == a.request("/api/workspace/download/project")
            assert a.describe()["revision"] == reply["revision"]
            a.request("/api/workspace/undo", b"")
            assert a.describe()["materials"][0]["youngsModulus"] == 1000
            # An acknowledged retry after undo returns its original receipt; it does not redo.
            retried = json.loads(a.request("/api/workspace/commands/material", command, headers=retry_headers))
            assert retried["receipt"] == receipt and retried["revision"] == a.revision
            assert a.describe()["materials"][0]["youngsModulus"] == 1000
            a.request("/api/workspace/redo", b"")
            state = a.describe()
            assert state["currentCommandId"] == command["operationId"]
            material_id = state["materials"][0]["id"]
            assert receipt["materialId"] == material_id
            rename = {"operationId": str(uuid.uuid4()), "expectedRevision": a.revision,
                      "materialId": material_id, "newName": "ALLOY"}
            rename_reply = json.loads(a.request("/api/workspace/commands/material/rename", rename))
            state = a.describe()
            assert state["materials"][0]["name"] == "ALLOY" and state["materials"][0]["id"] == material_id
            assert all(section["material"] == "ALLOY" for section in state["sections"])
            preserved = a.request("/api/workspace/download/project")
            b.request("/api/workspace/import", preserved, headers={"X-File-Extension": ".pma"})
            assert b.describe()["materials"][0]["id"] == material_id
            assert json.loads(b.request("/api/workspace/commands")) == []
            b.edit(action="material-save", name="ALLOY", e=999, nu=0)
            assert a.describe()["materials"][0]["youngsModulus"] == 2000
            assert a.request("/api/workspace/download/project") == preserved
            a.request("/api/workspace/commands/material/rename", dict(rename, operationId=str(uuid.uuid4()), expectedRevision=a.revision, newName="Bad Name"), expected=400)
            a.request("/api/workspace/commands/material", {"operationId": str(uuid.uuid4()), "expectedRevision": a.revision,
                      "materialId": material_id, "materialName": "ELASTIC", "youngsModulus": 2001}, expected=409)
            a.request("/api/workspace/undo", b"")
            assert a.describe()["materials"][0]["name"] == "ELASTIC"
            retried = json.loads(a.request("/api/workspace/commands/material/rename", rename,
                                headers={"If-Match": str(rename["expectedRevision"])}))
            assert retried["receipt"] == rename_reply["receipt"]
            assert a.describe()["materials"][0]["name"] == "ELASTIC"
            a.request("/api/workspace/redo", b"")
            a.describe()
            a.request("/api/workspace/commands/material", {"operationId": str(uuid.uuid4()), "expectedRevision": a.revision,
                      "materialId": material_id, "youngsModulus": 2000})
            a.describe()
            a.request("/api/workspace/commands/material/rename", {"operationId": str(uuid.uuid4()), "expectedRevision": a.revision,
                      "materialId": material_id, "newName": "ELASTIC"})
            assert a.describe()["materials"][0]["id"] == material_id
            material_lifecycle(a)
            a.edit(action="nodeset-add", name="WEB_RIGHT", nodes=[2, 3, 6, 7])
            a.edit(action="support-add", name="Fixed", step="load-1", region="LEFT")
            a.edit(action="force-add", name="Pull", step="load-1", region="WEB_RIGHT", f1=.25, f2=0, f3=0)
            deck = a.request("/api/workspace/download/input")
            assert b"*Cload" in deck or b"*CLOAD" in deck
            assert b"WEB_RIGHT" in deck and b"2000" in deck
            a.request("/api/workspace/undo", b"")
            assert not a.describe()["forces"]
            a.request("/api/workspace/redo", b"")
            assert len(a.describe()["forces"]) == 1
            frd = (ROOT / "portable/tests/fixtures/calculix/cube.frd").read_bytes()
            a.request("/api/workspace/results", frd)
            state = a.describe()
            field = next(i for i, item in enumerate(state["fields"]) if item["name"] == "DISP" and item["component"] == "U1")
            scene = json.loads(a.request("/api/workspace/scene", {"revision": a.revision, "field": field, "deformationScale": 100}))
            scene_path = "/api/v1/scenes/" + scene["sceneId"]
            manifest = json.loads(a.request(scene_path + "/manifest"))
            assert manifest["field"]["component"] == "U1" and scene["points"] == 8
            for resource in manifest["resources"]:
                payload = a.request(resource["uri"])
                assert len(payload) == resource["byteLength"]
                assert hashlib.sha256(payload).hexdigest().lower() == resource["sha256"].lower()
            b.request(scene_path + "/manifest", expected=404)
            anonymous.request(scene_path + "/manifest", expected=401)
            a.request("/api/v1/host/scene", b"forbidden", expected=404)
            tetra = (ROOT / "portable/tests/fixtures/import/tetra.inp").read_bytes()
            b.request("/api/workspace/import", tetra, headers={"X-File-Extension": ".inp", "X-Youngs-Modulus": "1000", "X-Poissons-Ratio": "0"})
            b.describe()
            unchanged = b.request("/api/workspace/download/project")
            b.request("/api/workspace/results", frd, expected=400)
            assert unchanged == b.request("/api/workspace/download/project")
            # A second real renderer cannot overwrite the first workspace's scene.
            b.request("/api/workspace/scene", {"revision": b.revision})
            assert json.loads(a.request(scene_path + "/manifest"))["field"]["component"] == "U1"
            solved = a.request("/api/workspace/download/project")
            a.edit(action="material-save", name="ELASTIC", e=2500, nu=0)
            assert not a.describe()["fields"]
            a.request(scene_path + "/manifest", expected=404)
            a.request("/api/workspace/undo", b"")
            assert a.describe()["fields"] and solved == a.request("/api/workspace/download/project")
            a.request("/api/workspace/import", b"not a project", headers={"X-File-Extension": ".pma"}, expected=400)
            assert solved == a.request("/api/workspace/download/project")
            print("PASS: browser ownership, CSRF, imports, material create/assign/remove lifecycle, persisted IDs, rename/reference repair, typed commands/receipts, stale/conflicting/unknown requests, retries after undo/recreation, production export, FRD compatibility, result invalidation, resource hashes and two isolated renderers.")
        finally:
            process.terminate()
            try:
                process.wait(timeout=20)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait()


if __name__ == "__main__":
    main()
