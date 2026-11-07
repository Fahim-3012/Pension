import { MIN_AGE, MAX_YEAR, ageAt } from './constants.js'

const today = () => new Date().toISOString().slice(0, 10)
const norm = (s) => (s || '').trim().toLowerCase().split(/\s+/).join(' ')

// Returns { s: stepNumber, field: fieldName, m: message } for the first problem, or null.
export function checkSignup(f) {
  const e = (s, field, m) => ({ s, field, m })
  // step 1 - personal
  if (!f.fullName?.trim()) return e(1, 'fullName', 'Full name is required')
  if (!/^(\d{10}|\d{13}|\d{17})$/.test(f.nid || '')) return e(1, 'nid', 'NID must be 10, 13 or 17 digits')
  if (!/^01[3-9]\d{8}$/.test(f.phone || '')) return e(1, 'phone', 'Phone must be a Bangladesh mobile number like 01712345678')
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]+$/.test(f.email || '')) return e(1, 'email', 'Enter a valid email address')
  if (!f.dateOfBirth) return e(1, 'dateOfBirth', 'Date of birth is required')
  if (f.dateOfBirth > today()) return e(1, 'dateOfBirth', 'Date of birth cannot be in the future')
  if (f.nid.length === 17 && f.nid.slice(0, 4) !== f.dateOfBirth.slice(0, 4)) return e(1, 'nid', 'A 17-digit NID must start with your birth year')
  if (!f.gender) return e(1, 'gender', 'Select gender')
  if (!f.address?.trim()) return e(1, 'address', 'Address is required')
  // step 2 - pension and bank
  if (!/^[A-Za-z0-9-]{6,20}$/.test(f.pensionId || '')) return e(2, 'pensionId', 'Pension ID must be 6-20 letters, numbers or hyphens')
  if (!f.pensionType) return e(2, 'pensionType', 'Select a pension type')
  if (!f.retirementDate || f.retirementDate <= f.dateOfBirth) return e(2, 'retirementDate', 'Retirement date must be after date of birth')
  if (Number(f.retirementDate.slice(0, 4)) > MAX_YEAR) return e(2, 'retirementDate', `Year limit is ${MAX_YEAR}`)
  const age = ageAt(f.dateOfBirth, f.retirementDate), min = MIN_AGE[f.pensionType] ?? 0
  if (age < min) return e(2, 'retirementDate', `Minimum age for ${f.pensionType} is ${min} years. Age at retirement: ${age}`)
  if (!f.nomineeName?.trim()) return e(2, 'nomineeName', 'Nominee name is required')
  if (!f.nomineeRelation) return e(2, 'nomineeRelation', 'Select nominee relation')
  if (!f.bankType) return e(2, 'bankType', 'Select bank type')
  if (!f.bankName) return e(2, 'bankName', 'Select bank name')
  if (!f.branch?.trim()) return e(2, 'branch', 'Branch is required')
  const acc = (f.accountNumber || '').trim()
  if (f.bankType === 'Mobile Financial Service') {
    if (!/^01[3-9]\d{8}$/.test(acc)) return e(2, 'accountNumber', 'Mobile banking number must be a Bangladesh mobile number like 01712345678')
  } else if (!/^\d{8,20}$/.test(acc)) return e(2, 'accountNumber', 'Account number must be 8 to 20 digits')
  if (norm(f.accountHolder) !== norm(f.fullName)) return e(2, 'accountHolder', 'Account holder name must match your full name')
  // step 3 - security
  if (!/^(?=.*[A-Z])(?=.*\d).{8,}$/.test(f.password || '')) return e(3, 'password', 'Password needs at least 8 characters, 1 capital letter and 1 digit')
  if (f.password !== f.confirmPassword) return e(3, 'confirmPassword', 'Passwords do not match')
  if (!f.acceptedTerms) return e(3, 'acceptedTerms', 'You must accept the Terms and Privacy Policy')
  return null
}

// Maps an error message coming from the server (duplicate NID, phone ...) to the field to mark red.
export function fieldFromServerMessage(m = '') {
  const t = m.toLowerCase()
  const rules = [
    ['pension id', 2, 'pensionId'], ['nid', 1, 'nid'], ['phone', 1, 'phone'], ['email', 1, 'email'],
    ['date of birth', 1, 'dateOfBirth'], ['minimum age', 2, 'retirementDate'], ['retirement', 2, 'retirementDate'],
    ['year limit', 2, 'retirementDate'], ['nominee', 2, 'nomineeName'], ['account holder', 2, 'accountHolder'],
    ['account number', 2, 'accountNumber'], ['mobile banking', 2, 'accountNumber'], ['bank', 2, 'bankName'],
    ['password', 3, 'password'], ['terms', 3, 'acceptedTerms']
  ]
  const r = rules.find(([k]) => t.includes(k))
  return r ? { s: r[1], field: r[2] } : null
}
