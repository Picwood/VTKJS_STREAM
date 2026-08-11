import { validateManifest } from './manifestValidator';
import type { ProtocolMessage, ResourceDescriptor, SceneManifest, SessionCreated } from './protocol';
import { decodeResource, ResourceValidationError, verifySha256 } from './resourceDecoder';
import { decodeSectionGeometry, type SectionGeometryRequest, type SectionGeometryResult } from './sectionGeometryBinary';

export class RenderApi {
  readonly #baseUrl: string;
  #accessToken: string | null = null;

  public constructor(baseUrl: string) {
    const parsed = new URL(baseUrl);
    if (parsed.protocol !== 'http:' || parsed.hostname !== '127.0.0.1') {
      throw new Error('The prototype server URL must use HTTP on 127.0.0.1.');
    }
    this.#baseUrl = parsed.href.replace(/\/$/, '');
  }

  public async createSession(signal?: AbortSignal): Promise<SessionCreated> {
    const response = await fetch(`${this.#baseUrl}/api/v1/sessions`, {
      method: 'POST',
      credentials: 'include',
      signal: signal ?? null,
    });
    await ensureSuccess(response);
    const session = await response.json() as SessionCreated;
    this.#accessToken = session.accessToken;
    return session;
  }

  public async getManifest(sceneId: string, signal?: AbortSignal): Promise<SceneManifest> {
    const response = await this.#fetch(`/api/v1/scenes/${sceneId}/manifest`, { signal: signal ?? null });
    return validateManifest(await response.json());
  }

  public async getResource(descriptor: ResourceDescriptor, signal?: AbortSignal) {
    for (let attempt = 0; attempt < 2; attempt += 1) {
      const response = await this.#fetch(descriptor.uri, { signal: signal ?? null });
      const buffer = await response.arrayBuffer();
      try {
        await verifySha256(descriptor, buffer);
        return decodeResource(descriptor, buffer);
      } catch (error) {
        if (!(error instanceof ResourceValidationError) || attempt === 1) {
          throw error;
        }
      }
    }
    throw new Error(`Resource ${descriptor.resourceId} failed integrity validation after retry.`);
  }

  public async sendCommand<TPayload = unknown>(
    sceneId: string,
    sceneRevision: number,
    type: string,
    payload: object = {},
    signal?: AbortSignal,
  ): Promise<ProtocolMessage<TPayload>> {
    const command: ProtocolMessage<object> = {
      protocol: '1.0',
      messageId: crypto.randomUUID(),
      correlationId: null,
      sceneId,
      sceneRevision,
      type,
      sentUtc: new Date().toISOString(),
      payload,
    };
    const response = await this.#fetch(`/api/v1/scenes/${sceneId}/commands`, {
      method: 'POST',
      body: JSON.stringify(command),
      headers: { 'Content-Type': 'application/json' },
      signal: signal ?? null,
    });
    return await response.json() as ProtocolMessage<TPayload>;
  }

  public async resolveSection(sceneId: string, request: SectionGeometryRequest,
    signal?: AbortSignal): Promise<SectionGeometryResult> {
    const response = await this.#fetch(`/api/v1/scenes/${sceneId}/section`, {
      method: 'POST',
      body: JSON.stringify(request),
      headers: { 'Content-Type': 'application/json' },
      signal: signal ?? null,
    });
    const contentType = response.headers.get('Content-Type')?.split(';', 1)[0];
    if (contentType !== 'application/vnd.prepomax.section-geometry-v1')
      throw new Error('The section geometry response has an unexpected content type.');
    return decodeSectionGeometry(await response.arrayBuffer());
  }

  public connectEvents(
    sceneId: string,
    onMessage: (message: ProtocolMessage) => void,
    onOpen: () => void,
    onError: (message: string) => void,
    onClose: () => void,
  ): WebSocket {
    const eventsUrl = new URL(`${this.#baseUrl}/api/v1/scenes/${sceneId}/events`);
    eventsUrl.protocol = 'ws:';
    const socket = new WebSocket(eventsUrl);
    socket.addEventListener('open', onOpen);
    socket.addEventListener('message', (event) => {
      try {
        const message = JSON.parse(String(event.data)) as ProtocolMessage;
        if (message.protocol !== '1.0' || message.sceneId !== sceneId) {
          throw new Error('Received an event for an unsupported protocol or scene.');
        }
        onMessage(message);
      } catch (error) {
        onError(error instanceof Error ? error.message : String(error));
      }
    });
    socket.addEventListener('error', () => onError('The scene event connection failed.'));
    socket.addEventListener('close', onClose);
    return socket;
  }

  async #fetch(path: string, init: RequestInit): Promise<Response> {
    if (!this.#accessToken) {
      throw new Error('A render session has not been created.');
    }
    const response = await fetch(`${this.#baseUrl}${path}`, {
      ...init,
      headers: { ...init.headers, 'X-PrePoMax-Session': this.#accessToken },
    });
    await ensureSuccess(response);
    return response;
  }
}

async function ensureSuccess(response: Response): Promise<void> {
  if (response.ok) {
    return;
  }
  let code = `http-${response.status}`;
  let message = response.statusText;
  try {
    const error = await response.json() as { code?: string; message?: string };
    code = error.code ?? code;
    message = error.message ?? message;
  } catch {
    // Preserve the safe HTTP fallback when the response is not JSON.
  }
  throw Object.assign(new Error(`${code}: ${message}`), { code });
}
