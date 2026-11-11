"""Immutable project snapshots for GTK editing, save, undo and redo. No GUI dependencies."""
import json
import os
from pathlib import Path
import shutil
import tempfile
import uuid


class EditSession:
    def __init__(self, source, root):
        self.source = Path(source).resolve()
        Path(root).mkdir(parents=True, exist_ok=True)
        self.directory = Path(tempfile.mkdtemp(prefix='session-', dir=root))
        self.history = [self.source]
        self.index = 0
        self.saved = self.current
        self.source_stamp = self.stamp(self.source)
        self.pending = None

    @staticmethod
    def stamp(path):
        stat = Path(path).stat()
        return stat.st_dev, stat.st_ino, stat.st_mtime_ns, stat.st_size

    @property
    def current(self):
        return self.history[self.index]

    @property
    def dirty(self):
        return self.current != self.saved

    def freeze_baseline(self):
        # Snapshot the baseline before Save can ever replace the opened file.
        if self.history[0].parent != self.directory:
            baseline = self.directory / 'baseline.pma'
            before = self.stamp(self.history[0])
            if self.history[0] == self.source and before != self.source_stamp:
                raise OSError('The opened project changed on disk. Reopen it before editing.')
            shutil.copyfile(self.history[0], baseline)
            if self.stamp(self.history[0]) != before:
                baseline.unlink(missing_ok=True)
                raise OSError('The opened project changed while preparing the edit.')
            if self.saved == self.history[0]:
                self.saved = baseline
            self.history[0] = baseline

    def reserve(self):
        self.freeze_baseline()
        if self.pending:
            self.pending.unlink(missing_ok=True)
        self.pending = self.directory / (uuid.uuid4().hex + '.pma')
        return self.pending

    def commit(self, path):
        path = Path(path).resolve()
        if path != self.pending or not path.is_file():
            raise ValueError('Only a completed edit snapshot can enter the session history.')
        self.history[self.index + 1:] = [path]
        self.index += 1
        self.pending = None
        self.journal()

    def move(self, index):
        if not 0 <= index < len(self.history):
            raise ValueError('There is no edit at this position.')
        self.index = index
        self.journal()

    def save(self, destination=None):
        self.freeze_baseline()
        destination = Path(destination).resolve() if destination else self.source
        if destination.suffix.lower() != '.pma':
            raise ValueError('Choose a project filename ending in .pma.')
        if destination.is_relative_to(self.directory):
            raise ValueError('Save the project outside the temporary editing session directory.')
        overwrite = destination == self.source
        if overwrite and self.stamp(destination) != self.source_stamp:
            raise OSError('The project changed on disk. Use Save as to keep both versions.')
        if not overwrite and destination.exists():
            raise FileExistsError('Choose a new project filename.')
        fd, temporary = tempfile.mkstemp(prefix='.prepomax-save-', suffix='.pma', dir=destination.parent)
        try:
            with os.fdopen(fd, 'wb') as output, self.current.open('rb') as source:
                shutil.copyfileobj(source, output)
                output.flush()
                os.fsync(output.fileno())
            if overwrite:
                if self.stamp(destination) != self.source_stamp:
                    raise OSError('The project changed while saving. Use Save as.')
                os.replace(temporary, destination)
            else:
                os.link(temporary, destination)
            self.source = destination
            self.source_stamp = self.stamp(destination)
            self.saved = self.current
            self.journal()
        finally:
            Path(temporary).unlink(missing_ok=True)

    def journal(self):
        temporary = self.directory / 'session.json.tmp'
        temporary.write_text(json.dumps({'source': str(self.source), 'history': [str(path) for path in self.history],
                                         'index': self.index, 'saved': str(self.saved)}, indent=2))
        temporary.replace(self.directory / 'session.json')

    def close(self, discard=False):
        if discard or not self.dirty:
            shutil.rmtree(self.directory, ignore_errors=True)
