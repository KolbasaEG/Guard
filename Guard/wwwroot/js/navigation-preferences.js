const prefix = 'guard.navigation.favorites.v1.';

export function load(userId) {
  try {
    const value = JSON.parse(localStorage.getItem(prefix + userId) ?? '[]');
    return Array.isArray(value) ? value.filter(path => typeof path === 'string').slice(0, 200) : [];
  } catch {
    return [];
  }
}

export function save(userId, paths) {
  try {
    localStorage.setItem(prefix + userId, JSON.stringify(paths));
    return true;
  } catch {
    return false;
  }
}
