import { api } from './client'

export type AttachmentOwnerType = 'work_order' | 'quote' | 'vehicle'
export type AttachmentStatus = 'pending_upload' | 'ready'

export interface AttachmentDto {
  id: string
  ownerType: AttachmentOwnerType
  ownerId: string
  contentType: string
  sizeBytes: number
  isInternal: boolean
  status: AttachmentStatus
  downloadUrl: string | null
  createdBy: string | null
  createdAt: string
}

export interface AttachmentUploadUrlResponse {
  attachmentId: string
  uploadUrl: string
}

export const attachmentsApi = {
  requestUploadUrl: (
    ownerType: AttachmentOwnerType,
    ownerId: string,
    contentType: string,
    size: number,
    isInternal = false,
  ) =>
    api.post<AttachmentUploadUrlResponse>('/api/v1/attachments/upload-url', {
      ownerType,
      ownerId,
      contentType,
      size,
      isInternal,
    }, true),

  confirm: (id: string) =>
    api.post<AttachmentDto>(`/api/v1/attachments/${id}/confirm`, undefined, true),

  listForWorkOrder: (workOrderId: string) =>
    api.get<AttachmentDto[]>(`/api/v1/work-orders/${workOrderId}/attachments`),

  update: (id: string, isInternal: boolean) =>
    api.post<AttachmentDto>(`/api/v1/attachments/${id}`, { isInternal }, true),

  delete: (id: string) =>
    api.delete<void>(`/api/v1/attachments/${id}`, undefined, true),

  /** Sube un archivo en dos pasos: solicita URL → PUT directo a R2 → confirma. */
  upload: async (
    ownerType: AttachmentOwnerType,
    ownerId: string,
    file: File,
    isInternal = false,
  ): Promise<AttachmentDto> => {
    const { attachmentId, uploadUrl } = await attachmentsApi.requestUploadUrl(
      ownerType, ownerId, file.type, file.size, isInternal,
    )

    const putRes = await fetch(uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': file.type },
      body: file,
    })
    if (!putRes.ok) throw new Error('Error al subir el archivo al almacenamiento.')

    return attachmentsApi.confirm(attachmentId)
  },
}
