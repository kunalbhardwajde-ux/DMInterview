export function getErrorMessage(error, fallback = 'Something went wrong.') {
  if (typeof error === 'string') {
    return error
  }

  if (error instanceof Error) {
    return error.message
  }

  if (typeof error?.message === 'string') {
    return error.message
  }

  if (typeof error?.response?.data?.message === 'string') {
    return error.response.data.message
  }

  return fallback
}

export function buildQueryString(params) {
  const searchParams = new URLSearchParams()

  Object.entries(params).forEach(([key, value]) => {
    if (value === undefined || value === null || value === '') {
      return
    }

    searchParams.set(key, String(value))
  })

  return searchParams.toString()
}
