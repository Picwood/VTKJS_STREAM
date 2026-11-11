#!/usr/bin/env python3
"""Exercise branching history, atomic save, conflicts and recovery retention."""
from pathlib import Path
import tempfile
import unittest

from linux_edit_session import EditSession


class SessionChecks(unittest.TestCase):
    def test_edit_undo_redo_save_and_branch(self):
        with tempfile.TemporaryDirectory() as root:
            source = Path(root) / 'source.pma'
            source.write_bytes(b'original')
            session = EditSession(source, root)
            first = session.reserve()
            first.write_bytes(b'first')
            session.commit(first)
            self.assertEqual(source.read_bytes(), b'original')
            self.assertTrue(session.dirty)
            session.move(0)
            self.assertEqual(session.current.read_bytes(), b'original')
            self.assertFalse(session.dirty)
            session.move(1)
            session.save()
            self.assertEqual(source.read_bytes(), b'first')
            self.assertFalse(session.dirty)
            session.move(0)
            self.assertEqual(session.current.read_bytes(), b'original')
            self.assertTrue(session.dirty)
            second = session.reserve()
            second.write_bytes(b'branch')
            session.commit(second)
            self.assertEqual(session.history, [session.history[0], second])
            alternate = Path(root) / 'alternate.pma'
            session.save(alternate)
            self.assertEqual(source.read_bytes(), b'first')
            self.assertEqual(alternate.read_bytes(), b'branch')
            self.assertFalse(session.dirty)
            session.close()
            self.assertFalse(session.directory.exists())

    def test_failure_conflict_and_recovery(self):
        with tempfile.TemporaryDirectory() as root:
            source = Path(root) / 'source.pma'
            source.write_bytes(b'original')
            session = EditSession(source, root)
            reserved = session.reserve()
            with self.assertRaises(ValueError): session.commit(reserved)
            self.assertFalse(session.dirty)
            reserved.write_bytes(b'edited')
            session.commit(reserved)
            existing = Path(root) / 'existing.pma'
            existing.write_bytes(b'keep')
            with self.assertRaises(ValueError): session.save(Path(root) / 'wrong-format.pmx')
            with self.assertRaises(ValueError): session.save(session.directory / 'saved.pma')
            with self.assertRaises(FileExistsError): session.save(existing)
            self.assertEqual(existing.read_bytes(), b'keep')
            source.write_bytes(b'external-change')
            with self.assertRaises(OSError): session.save()
            self.assertTrue(session.dirty)
            self.assertFalse(list(Path(root).glob('.prepomax-save-*')))
            session.close()
            self.assertTrue((session.directory / 'session.json').exists())
            self.assertEqual(session.current.read_bytes(), b'edited')
            session.close(discard=True)
            self.assertFalse(session.directory.exists())

    def test_changed_baseline_is_not_silently_imported(self):
        with tempfile.TemporaryDirectory() as root:
            source = Path(root) / 'source.pma'
            source.write_bytes(b'original')
            session = EditSession(source, root)
            source.write_bytes(b'changed')
            with self.assertRaises(OSError): session.reserve()
            self.assertFalse(session.dirty)
            session.close()


if __name__ == '__main__':
    unittest.main()
