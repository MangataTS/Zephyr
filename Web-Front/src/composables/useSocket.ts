import { ref, onMounted, onUnmounted } from 'vue'

export function useSocket(roomId: string) {
  const socket = ref<WebSocket | null>(null)
  const connected = ref(false)

  onMounted(() => {
    const token = localStorage.getItem('auth_token')
    const proto = window.location.protocol === 'https:' ? 'wss:' : 'ws:'
    const host = window.location.host
    const url = `${proto}//${host}/ws/notes/${encodeURIComponent(roomId)}?token=${encodeURIComponent(token || '')}`

    try {
      socket.value = new WebSocket(url)
    } catch {
      connected.value = false
      return
    }

    socket.value.onopen = () => {
      connected.value = true
    }

    socket.value.onclose = () => {
      connected.value = false
    }
  })

  onUnmounted(() => {
    socket.value?.close()
    socket.value = null
  })

  return {
    socket,
    connected,
  }
}
